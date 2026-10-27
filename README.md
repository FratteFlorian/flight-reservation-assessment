# Flight reservation – coding exercise

A small booking API: flights, seats, reservations. It is a self-contained .NET 10 / EF Core /
SQLite application – no Docker, no database server, no frontend. A database file is created next
to the API on first start.

**Time budget:** about one hour. Please stop when the hour is up – a
half-finished solution is just as useful to us as a finished one, and the note counts as much as
the code.

**This is not a pass/fail test.** We are not looking for a score. We want to see how you move
through a codebase you have not seen before: how you work out what is expected, what you try,
what you discard, and whether the .NET and EF Core fundamentals are there. If something does not
work out, leave a short comment at the right place in the code saying what you would have done –
that tells us more about how you think than a green test does.

## Prerequisites

- .NET SDK 10 (`dotnet --version` should print `10.x`). SQLite is bundled.
- For the smoke runner: `curl` and `python3` (on Windows: Git Bash or WSL).

## Getting started

```bash
dotnet build FlightReservation.sln
dotnet test FlightReservation.sln
dotnet run --project src/FlightReservation.Api
```

Then try it by hand – the port is printed on startup:

```bash
curl -X POST http://localhost:5000/api/flights \
  -H 'Content-Type: application/json' \
  -d '{"number":"OS201","origin":"VIE","destination":"FRA","departureAt":"2026-11-03T09:30:00Z","capacity":3}'

curl -X POST http://localhost:5000/api/flights/OS201/reservations \
  -H 'Content-Type: application/json' \
  -d '{"passengerName":"Anna Weber","seats":2}'

curl http://localhost:5000/api/flights/OS201
```

## The domain in short

A **flight** has a number, a route and a capacity. A **reservation** blocks seats on one flight
for one passenger and has a booking code. It starts as `Held`, can be `Confirmed`, and can be
`Cancelled`. `seatsAvailable` is what the flight has left **right now**.

| Endpoint | Meaning |
|---|---|
| `POST /api/flights` | create a flight |
| `GET /api/flights/{number}` | flight with its reservations and the free seats |
| `POST /api/flights/{number}/reservations` | book seats, they are `Held` |
| `GET /api/reservations/{reference}` | read a booking by its code |
| `POST /api/reservations/{reference}/confirm` | `Held` → `Confirmed` |
| `POST /api/reservations/{reference}/cancel` | give the seats back |

Domain errors become `404`, `409` and `400` as ProblemDetails.

## What you get

| Project | State |
|---|---|
| `src/FlightReservation.Domain` | Entities, contracts, exceptions, the service interface. **Complete.** |
| `src/FlightReservation.Data` | `BookingDbContext` (EF model) and `BookingService`. **This is where you work.** |
| `src/FlightReservation.Api` | Minimal API endpoints and the exception handler. **Complete.** |
| `tests/FlightReservation.Tests` | The specification. **Do not change or delete these tests.** |
| `scripts/smoke.sh` | End-to-end checks over HTTP with curl. |

The tests describe the expected behaviour: the name says what is checked, and a failing test is
something that is not implemented yet. Most of the suite is green already – that is your safety
net while you work, and the best place to see which style the code is written in.

## What to do

Read `src/FlightReservation.Data/BookingService.cs` and work through the failing tests.

**The requirement: a flight cannot be overbooked.** A booking that does not fit must fail with
`FlightFullException` (409) and store nothing. Seats are taken while a reservation is `Held` or
`Confirmed`; a cancelled reservation gives them back.

Contracts that the existing tests rely on, so they do not become a guessing game:

- `seatsAvailable` is `capacity` minus the seats of all `Held` and `Confirmed` reservations.
- A booking that exactly fills the last free seats is allowed – only going beyond is refused.
- A cancelled reservation stays in the flight's reservation list; only its seats are free again.
- `FlightFullException` carries the flight number, the capacity, the seats already taken and the
  requested seats. The tests check those four values.
- A seat count below one, a blank passenger name or a blank flight number is an
  `ArgumentException` (`400`).
- Statuses travel as text: `Held`, `Confirmed`, `Cancelled`. Booking codes are generated for you.

**If you get through it quickly:** the check and the insert of the new reservation belong in one
transaction – with two separate `SaveChanges` there is a moment in which two callers can both see
the same free seat. Nothing tests that; it is the kind of thing we would talk about.

## How to check your work

```bash
dotnet test FlightReservation.sln     # 26 tests; two of them fail on the untouched starter
scripts/smoke.sh                      # 33 checks over HTTP with curl; three of them fail
```

The smoke runner builds the solution, starts the API on a throwaway SQLite file, walks the whole
flow and prints one line per check. Its failing checks are a second, more readable view of the
same three requirements. Add your own checks or tests if you want to pin something down.

## Ground rules

- Do not change or delete the tests, and do not weaken them. If you think a test is wrong, leave
  it as it is and say so in your note.
- Do not change the public contracts in `FlightReservation.Domain` (records, exception types, the
  service interface), the endpoint routes or the EF model. `FlightReservation.Api` and
  `FlightReservation.Domain` should stay untouched.
- Adding your own tests is welcome.
- Work in a git repository and commit in steps – we look at the history as well as the result.
- Use whatever tools you normally use, including AI assistants and the documentation. We will ask
  you to explain one or two decisions from your own diff afterwards, so make sure you can stand
  behind every line you hand in.
- Leave the work in a state where your path is visible: a comment where you stopped, a note about
  what you rejected and why.
- If something is ambiguous, write down the assumption you made and keep going.

## How to hand it in

Fork this repository and open a **pull request** against `main`:

```bash
git clone <the URL of this repository>
cd flight-reservation-assessment
git switch -c booking-capacity
# work, commit in small steps

git push -u origin booking-capacity
```

Then open the pull request. Please keep `main` untouched and put the work on its own branch – we
read the commit history as well as the result.

In the pull request description, add the short note – at most one page:

- how you went about it: what you read first and how you found out what the tests expect,
- what you changed and why,
- what you tried and discarded, and where you got stuck,
- what you assumed,
- what you would improve next, and what you would test if you had another hour,
- anything you noticed in the existing code that you did not change.

A short conversation afterwards is part of the exercise: we will walk through your diff together
and ask you to explain one or two decisions. Nothing to prepare – it is about your own work.
