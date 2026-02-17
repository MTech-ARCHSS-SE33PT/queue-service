# Queue Management Service

Purpose: Core queue lifecycle and operations.

## Completed Use Cases

- UC-Q1: Create Queue (Admin creates a queue for a location/service type)
- UC-Q3: Generate Ticket / Join Queue (Staff creates ticket for walk-in OR system creates from appointment)
- UC-Q4: Call Next Ticket (Staff advances queue)
- UC-Q6: Real-time Queue Status (Public dashboard reads current serving number + waiting list)

## Incomplete Use Cases

- UC-Q2: Configure Capacity / Service Counters (Admin sets number of counters, operating hours)  
  Currently, admin can only configure queue with number of counters (no operating hours, etc.)

- UC-Q5: Update Ticket Status (Serving, Skipped, Cancelled, Completed)  
  Ticket status can only be updated to complete (no skipped, cancelled, etc.)

## Notes

Currently using a mock in-memory event publisher (API calls) for adding a dummy appointment and performing basic operations.
