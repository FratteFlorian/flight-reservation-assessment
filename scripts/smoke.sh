#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Flight reservation end-to-end smoke test
#
# Builds the solution, starts the API against a throwaway SQLite file, walks the
# booking flow with curl and reports every check. Nothing outside this folder
# is touched and the scratch database is removed afterwards.
#
# Usage:
#   scripts/smoke.sh
#
# Requirements: dotnet, curl, python3.
# ---------------------------------------------------------------------------

set -u

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BASE=http://localhost:5199
DBFILE=${TMPDIR:-/tmp}/flight-reservation-smoke.db
LOG=${TMPDIR:-/tmp}/flight-reservation-smoke-api.log
BUILDLOG=${TMPDIR:-/tmp}/flight-reservation-smoke-build.log
PIDFILE=${TMPDIR:-/tmp}/flight-reservation-smoke-api.pid
API_DLL="$ROOT/src/FlightReservation.Api/bin/Debug/net10.0/FlightReservation.Api.dll"

PASS=0
FAIL=0
FAILED_CHECKS=()

ok()    { PASS=$((PASS+1)); echo "    PASS: $1"; }
bad()   { FAIL=$((FAIL+1)); FAILED_CHECKS+=("$1"); echo "    FAIL: $1"; }
check() { if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 (expected [$3], got [$2])"; fi; }
code()  { curl -s -o /dev/null -w '%{http_code}' "$@"; }
post()  { curl -s -X POST "$BASE$1" -H 'Content-Type: application/json' ${2:+-d "$2"}; }
get()   { curl -s "$BASE$1"; }
json()  { python3 -c 'import json,sys; print(json.load(sys.stdin).get(sys.argv[1], ""))' "$1"; }

stop_api() {
  if [ -f "$PIDFILE" ]; then
    kill "$(cat "$PIDFILE")" 2>/dev/null || true
    rm -f "$PIDFILE"
    sleep 1
  fi
}
trap stop_api EXIT

start_api() {
  stop_api
  rm -f "$DBFILE" "$DBFILE-shm" "$DBFILE-wal" "$LOG"
  ( cd "$ROOT" && Database__Path="$DBFILE" dotnet "$API_DLL" --urls "$BASE" > "$LOG" 2>&1 & echo $! > "$PIDFILE" )

  for _ in $(seq 1 60); do
    curl -sf "$BASE/health" > /dev/null && return 0
    sleep 1
  done

  echo "  the API did not answer on $BASE; last lines of $LOG:"
  tail -20 "$LOG"
  exit 1
}

# ---------------------------------------------------------------------------
echo "== Setup: build and start the API =="
if ! dotnet build "$ROOT/FlightReservation.sln" --nologo -v quiet > "$BUILDLOG" 2>&1; then
  echo "  the solution does not build:"
  tail -20 "$BUILDLOG"
  exit 1
fi
start_api
check "API is up" "$(code "$BASE/health")" "200"

# ---------------------------------------------------------------------------
echo "== Scenario: book a seat on a flight =="
check "flight OS201 created" \
  "$(code -X POST "$BASE/api/flights" -H 'Content-Type: application/json' \
     -d '{"number":"OS201","origin":"VIE","destination":"FRA","departureAt":"2026-11-03T09:30:00Z","capacity":3}')" "201"
check "OS201 has three free seats" "$(get /api/flights/OS201 | json seatsAvailable)" "3"
check "the same flight number is rejected" \
  "$(code -X POST "$BASE/api/flights" -H 'Content-Type: application/json' \
     -d '{"number":"OS201","origin":"VIE","destination":"FRA","departureAt":"2026-11-03T09:30:00Z","capacity":3}')" "409"

BOOKING="$(post /api/flights/OS201/reservations '{"passengerName":"Anna Weber","seats":1}')"
REFERENCE="$(echo "$BOOKING" | json reference)"
check "booking is held" "$(echo "$BOOKING" | json status)" "Held"
check "one seat is taken" "$(echo "$BOOKING" | json seatsAvailable)" "2"
check "the booking code has six characters" "${#REFERENCE}" "6"

check "OS201 shows two free seats" "$(get /api/flights/OS201 | json seatsAvailable)" "2"
check "the reservation can be read back" "$(get "/api/reservations/$REFERENCE" | json status)" "Held"
check "an unknown flight is a 404" "$(code "$BASE/api/flights/XX999")" "404"
check "an unknown reservation is a 404" "$(code "$BASE/api/reservations/ZZZZZZ")" "404"
check "a booking without seats is a 400" \
  "$(code -X POST "$BASE/api/flights/OS201/reservations" -H 'Content-Type: application/json' -d '{"passengerName":"Anna Weber","seats":0}')" "400"
check "a booking without a name is a 400" \
  "$(code -X POST "$BASE/api/flights/OS201/reservations" -H 'Content-Type: application/json' -d '{"passengerName":"  ","seats":1}')" "400"
check "a booking on an unknown flight is a 404" \
  "$(code -X POST "$BASE/api/flights/XX999/reservations" -H 'Content-Type: application/json' -d '{"passengerName":"Anna Weber","seats":1}')" "404"

# ---------------------------------------------------------------------------
echo "== Scenario: a flight cannot be overbooked =="
post /api/flights '{"number":"OS202","origin":"VIE","destination":"LHR","departureAt":"2026-11-03T14:00:00Z","capacity":2}' > /dev/null
FULL="$(post /api/flights/OS202/reservations '{"passengerName":"Boris Kern","seats":2}')"
check "OS202 is full" "$(echo "$FULL" | json seatsAvailable)" "0"
check "one more seat is refused" \
  "$(code -X POST "$BASE/api/flights/OS202/reservations" -H 'Content-Type: application/json' -d '{"passengerName":"Clara Muth","seats":1}')" "409"
check "the refused booking was not stored" "$(get /api/flights/OS202 | json seatsAvailable)" "0"
check "booking three seats on two is refused" \
  "$(code -X POST "$BASE/api/flights/OS202/reservations" -H 'Content-Type: application/json' -d '{"passengerName":"Clara Muth","seats":3}')" "409"

# ---------------------------------------------------------------------------
echo "== Scenario: cancelling gives the seats back =="
post /api/flights '{"number":"OS205","origin":"ZRH","destination":"FRA","departureAt":"2026-11-03T18:40:00Z","capacity":2}' > /dev/null
CANCELME="$(post /api/flights/OS205/reservations '{"passengerName":"Boris Kern","seats":2}')"
CANCELME_REFERENCE="$(echo "$CANCELME" | json reference)"
check "cancelling returns the cancelled booking" "$(post "/api/reservations/$CANCELME_REFERENCE/cancel" | json status)" "Cancelled"
check "the seats are free again" "$(get /api/flights/OS205 | json seatsAvailable)" "2"
check "the two seats can be booked again" \
  "$(code -X POST "$BASE/api/flights/OS205/reservations" -H 'Content-Type: application/json' -d '{"passengerName":"Clara Muth","seats":2}')" "201"
check "OS205 is full again" "$(get /api/flights/OS205 | json seatsAvailable)" "0"
check "the cancelled booking is still in the flight" "$(get /api/flights/OS205 | python3 -c 'import json,sys; print(len([r for r in json.load(sys.stdin)["reservations"] if r["status"]=="Cancelled"]))')" "1"
check "cancelling an unknown booking is a 404" "$(code -X POST "$BASE/api/reservations/ZZZZZZ/cancel")" "404"

# ---------------------------------------------------------------------------
echo "== Scenario: a cancelled booking stays cancelled =="
post /api/flights '{"number":"OS203","origin":"FRA","destination":"JFK","departureAt":"2026-11-04T07:15:00Z","capacity":1}' > /dev/null
CANCELLED="$(post /api/flights/OS203/reservations '{"passengerName":"Dora Lindt","seats":1}')"
CANCELLED_REFERENCE="$(echo "$CANCELLED" | json reference)"
check "OS203 booking is held" "$(echo "$CANCELLED" | json status)" "Held"
check "cancelling OS203 works" "$(post "/api/reservations/$CANCELLED_REFERENCE/cancel" | json status)" "Cancelled"
check "OS203 has its seat back" "$(get /api/flights/OS203 | json seatsAvailable)" "1"
check "a cancelled booking cannot be confirmed" "$(code -X POST "$BASE/api/reservations/$CANCELLED_REFERENCE/confirm")" "409"
check "a cancelled booking stays cancelled" "$(get "/api/reservations/$CANCELLED_REFERENCE" | json status)" "Cancelled"

# ---------------------------------------------------------------------------
echo "== Scenario: cancelling twice is accepted =="
post /api/flights '{"number":"OS204","origin":"JFK","destination":"FRA","departureAt":"2026-11-05T20:00:00Z","capacity":1}' > /dev/null
TWICE="$(post /api/flights/OS204/reservations '{"passengerName":"Elias Roth","seats":1}')"
TWICE_REFERENCE="$(echo "$TWICE" | json reference)"
check "the second cancel is accepted" "$(code -X POST "$BASE/api/reservations/$TWICE_REFERENCE/cancel")" "200"
check "and again" "$(code -X POST "$BASE/api/reservations/$TWICE_REFERENCE/cancel")" "200"
check "the booking is still cancelled" "$(get "/api/reservations/$TWICE_REFERENCE" | json status)" "Cancelled"
check "the seat was not handed out twice" "$(get /api/flights/OS204 | json seatsAvailable)" "1"

# ---------------------------------------------------------------------------
echo "== Teardown =="
stop_api
rm -f "$DBFILE" "$DBFILE-shm" "$DBFILE-wal"

echo
echo "== Result: $PASS passed, $FAIL failed =="
if [ "$FAIL" -gt 0 ]; then
  printf '  failed: %s\n' "${FAILED_CHECKS[@]}"
  exit 1
fi
echo "ALL CHECKS PASSED"
