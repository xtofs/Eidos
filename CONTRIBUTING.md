# Contributing to EIDOS

Thank you for your interest in contributing to EIDOS. Please read this
document before submitting changes.

## Licensing of Contributions

This repository uses different licenses for different components. By
submitting a contribution, you agree to the following:

- **Contributions to `SPECIFICATION/`** are licensed under the
  [Creative Commons Attribution-ShareAlike 4.0 International](SPECIFICATION/LICENSE-SPEC.txt)
  (CC-BY-SA 4.0) license.
- **Contributions to `IMPLEMENTATION/`** are licensed under the
  [GNU Affero General Public License v3.0](IMPLEMENTATION/LICENSE-IMPL.txt)
  (AGPL-3.0).

### Dual Licensing Agreement

The EIDOS project offers the implementation to commercial customers under a
separate proprietary license (see
[COMMERCIAL/LICENSE-COMMERCIAL.txt](COMMERCIAL/LICENSE-COMMERCIAL.txt)).
To make this possible, **all contributors must agree that their contributions
may be dual-licensed**: distributed under the applicable open-source license
(CC-BY-SA 4.0 or AGPL-3.0) _and_ under the EIDOS commercial license.

By submitting a pull request, you confirm that:

1. You are the author of the contribution, or have the right to submit it.
2. You grant the EIDOS project maintainers a perpetual, worldwide,
   royalty-free right to license your contribution under both the applicable
   open-source license and the EIDOS commercial license.
3. You understand that your contribution remains available to the public
   under the open-source licenses.

If you cannot agree to these terms, please do not submit contributions.

## How to Submit Issues

1. Search the issue tracker for existing reports before opening a new one.
2. For specification issues, reference the affected section of
   `eidos-spec.md` or `eidos-semantics.md`.
3. For implementation issues, include a minimal reproduction, the runtime
   version, and the observed versus expected behavior.

## How to Submit Pull Requests

1. Fork the repository and create a feature branch from `main`.
2. Keep pull requests focused: one logical change per PR.
3. For specification changes, update the relevant examples and conformance
   tests under `SPECIFICATION/examples/` and `SPECIFICATION/tests/`.
4. For implementation changes, add or update tests under
   `IMPLEMENTATION/tests/` and ensure the test suite passes.
5. Describe the motivation and the effect of the change in the PR
   description, and link related issues.
6. A maintainer will review your PR; please respond to review feedback.

## Questions

Open a discussion or issue if anything in this document is unclear.
