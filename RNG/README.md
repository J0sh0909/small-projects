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

## Automated (script)

Run the bundled `setup.ps1`. It checks for **Git** and **Go**, installs whatever is missing via `winget`, fetches the project from the repo (if you don't already have it), builds `rng.exe`, and runs it once to verify.

From a local clone:

```powershell
cd RNG
.\setup.ps1
```

Or bootstrap it standalone in one line (downloads the repo for you):

```powershell
iwr -useb https://raw.githubusercontent.com/J0sh0909/small-projects/main/RNG/setup.ps1 | iex
```

> If PowerShell blocks the script, allow it for the current session:
> `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`

After setup, generate a password any time with:

```powershell
.\rng.exe
```

---

## Manual

### Requirements

- [Go](https://go.dev/dl/) 1.20 or newer (`go version` to check)

### Run without building

```powershell
cd RNG
go run rng.go
```

### Build a standalone executable

```powershell
cd RNG
go build -o rng.exe rng.go
.\rng.exe
```

The resulting `rng.exe` has no dependencies and can be copied anywhere.

---

## Customizing

The generator lives entirely in [rng.go](./rng.go):

- **Password length** — change the `rng(10)` call in `main()` to your desired length.
- **Character set** — edit the `charset` constant to add or remove allowed characters.

After editing, rebuild with `go build -o rng.exe rng.go` (or re-run `go run rng.go`).

