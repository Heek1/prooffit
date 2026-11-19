# 0009. Years of experience are counted in code

## Context

"3+ years of Python" is one of the most common requirements, and the answer is usually in the resume's dates rather than in its words. Working out "March 2019 to February 2022, then March 2022 to present" is arithmetic, which a language model does unreliably and code does exactly. The keyword engine did not do it at all: any mention of the technology confirmed the requirement.

## Decision

`Experience` reads dated periods from the resume ("March 2019 to February 2022", "Jan 2020 – present", "03/2019 - 02/2022", "2017 to 2018", Ukrainian months and "дотепер"). A start without a month counts from January and an end without one runs to December. A period is credited with the technologies named on its line and the lines under it, up to the next dated line or a blank one. Overlapping periods are counted once. Lines about study (degrees, courses) and lines that are not the candidate's own work are left out.

The keyword engine checks a requirement for years against those counts: fewer than asked makes it partial, with the counted time in the reason, and a requirement for years with no technology is checked against the total. The model is given the same counts in its prompt and told to use them rather than redo the arithmetic.

## Consequences

The years-arithmetic case in the evaluation went from 2 of 4 to 4 of 4 for the keyword engine, with no other case changing. A technology with no dated job behind it keeps its earlier verdict, because the resume may state the years in words ("three years of Django"), which the counter does not read. A resume laid out as a table, or one that lists technologies in a separate section away from the jobs, gets no per-technology counts, only a total.
