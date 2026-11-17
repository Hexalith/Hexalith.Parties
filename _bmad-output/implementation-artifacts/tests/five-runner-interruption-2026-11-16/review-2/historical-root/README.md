# Historical v1 runner-interruption evidence

The root-level receipts, implementation hashes, commands and verification describe
the original implementation seed. They are historical and do not qualify the
corrected review-loop source. Original failed receipts remain failed.

The current qualification packet is [review-1/README.md](review-1/README.md).
Its readable source, cohort, six-case matrix, commands and verification indexes
identify the authoritative unchanged implementation and executed results.

The original v1 README and checksum indexes are preserved byte-for-byte in
[review-1/historical-v1/](review-1/historical-v1/). The root checksum indexes are
regenerated for the retained packet after packaging; checksums establish byte
integrity and never promote historical evidence to current-source acceptance.

Detailed receipts use deterministic gzip. Decode a file with `gzip -cd FILE.gz`.
Private raw runtime logs and credentials remain outside this public packet.
