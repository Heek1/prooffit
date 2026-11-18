# 0007. No accounts; reports expire after a day

## Context

Asking someone trying a portfolio demo to sign in loses most of them, but without accounts a report is readable by anyone who has its address.

## Decision

There is no sign-in. Comparisons and their documents are deleted 24 hours after they are made, the report says when, and it offers to delete them at once. Requests are rate limited per client to protect the shared model quota.

## Consequences

The demo works in one click, and exposure is bounded in time. Private reports and a history across devices would need sign-in, which is planned with GitHub OAuth.
