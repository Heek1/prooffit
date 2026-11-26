# 0005. Analyses run in a background worker and report progress from the database

## Context

A comparison can take several seconds with a model, more on a cold free-tier server, which is too long to hold an HTTP request open.

## Decision

Creating an analysis stores it and returns 202. A hosted worker takes it from an in-memory queue, and unfinished analyses are requeued on startup. Progress is streamed as server-sent events read from the database rather than pushed from the worker.

## Consequences

Requests return at once, and progress keeps working if the API and the worker become separate processes. The queue was first kept in memory, so only one instance could run; record 0010 moved it into the database.
