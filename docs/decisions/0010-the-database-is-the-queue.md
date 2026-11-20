# 0010. The analyses table is the queue

Supersedes the in-memory queue in record 0005.

## Context

Analyses were handed to the worker through a channel in the process's memory. Only one instance of the API could run, since a second would never see the first one's analyses, and an analysis whose process died was picked up only when that same service restarted.

## Decision

The analyses table is the queue. A worker claims the oldest analysis that is waiting, or whose lease has run out, with one `UPDATE ... WHERE "Id" = (SELECT ... FOR UPDATE SKIP LOCKED LIMIT 1)`. The claim sets a two-minute lease and counts the attempt. While the analysis runs the worker renews the lease every thirty seconds, and on shutdown it hands the lease back. The database's clock decides when a lease has run out.

An analysis taken on more than three times is failed instead of tried again, since it is more likely the cause of the crashes than their victim. Workers poll every five seconds; creating an analysis wakes the local worker at once, so a single instance starts work without waiting.

No message broker was added: PostgreSQL is already there, the volume is small, and a claim in the same database as the row cannot disagree with it.

## Consequences

Any number of instances can share the work, each analysis runs once at a time, and an instance that dies loses at most two minutes before another one takes over. Analyses created on another instance start within five seconds rather than at once; LISTEN/NOTIFY would remove that delay. An analysis can run twice if a worker stalls past its lease without dying, so running one must stay safe to repeat, as it is now: it only writes its own row.
