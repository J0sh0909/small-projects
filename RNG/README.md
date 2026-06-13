
# RNG

A cryptographically secure random password generator written in Go. It uses Go's `crypto/rand` (not `math/rand`), so the output is suitable for real secrets not just toy randomness.

By default it prints a single 10-character password drawn from upper- and lower-case letters, digits, and a wide set of symbols:

```
abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+-=[]{};:,.?~
```

```
Generated password: gT4!q8$Lz#
```

---

## Customizing

The generator lives entirely in [rng.go](./rng.go):

- **Password length:** change the `rng(10)` call in `main()` to your desired length.
- **Character set:** edit the `charset` constant to add or remove allowed characters.

After editing, rebuild with `go build -o rng.exe rng.go` (or re-run `go run rng.go`).

