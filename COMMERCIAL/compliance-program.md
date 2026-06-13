# EIDOS Compliance Program

This document describes how organizations can certify that their products
conform to the EIDOS specification.

## Compliance Certification

Organizations holding a commercial license can certify compliance of their
implementations as follows:

1. **Self-assessment** — run the official EIDOS conformance test suite
   against your implementation and record the results.
2. **Submission** — submit the test results, along with the implementation
   version and the targeted specification version, to the EIDOS maintainers.
3. **Review** — the maintainers review the submission and may request
   clarifications or additional test runs.
4. **Certification** — upon successful review, the organization receives a
   compliance certificate for the specific implementation version and
   specification version, and may use the "EIDOS Compliant" designation for
   that version.

Certification is version-specific: a new certification is required for each
major implementation release or when targeting a new specification version.

## Versioning

The EIDOS specification follows semantic versioning:

- **Major versions** introduce breaking changes to syntax or semantics.
  Certifications do not carry over across major versions.
- **Minor versions** add backward-compatible features. Existing
  certifications remain valid; certified implementations may optionally
  re-certify to claim support for new features.
- **Patch versions** contain clarifications and errata only and do not
  affect certification status.

Each specification release is tagged in this repository, and the conformance
test suite is versioned in lockstep with the specification.

## Test Suites

Conformance test suites are provided as follows:

- The **public test suite** is included in this repository under
  `SPECIFICATION/tests/` and is available to everyone under CC-BY-SA 4.0.
- Commercial licensees receive the **extended certification suite**, which
  includes additional edge-case and stress tests used during the formal
  certification review.
- Test suites are distributed per specification version; licensees are
  notified when new suite versions are published.

For questions about the compliance program, contact the EIDOS maintainers.
