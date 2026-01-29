# Pupdate Unit Tests

This directory contains unit tests for the pupdate project using xUnit.

## Running Tests

```bash
dotnet test tests/pupdate.Tests/pupdate.Tests.csproj
```

## Test Coverage

### SemverUtil Tests (SemverUtilTests.cs)
- `FindSemver()` - Version extraction from various string formats
- `SemverCompare()` - Semantic version comparison logic
- Tests cover edge cases like 2-part versions, invalid inputs, and numeric comparisons

### Util Tests (UtilTests.cs)
- `WordWrap()` - Text wrapping with various widths and padding
- `GetExceptionMessage()` - Exception message extraction including nested exceptions

### Util Checksum Tests (UtilChecksumTests.cs)
- `CompareChecksum()` - CRC32 and MD5 hash validation
- Tests with temp files, case-insensitive comparison, and error handling

### ReverseComparer Tests (ReverseComparerTests.cs)
- `IComparer.Compare()` - Reverse comparison logic for sorting

## Test Statistics
- **Total Tests**: 39
- **Passing**: 39
- **Test Framework**: xUnit 2.9.2
- **Assertion Library**: FluentAssertions 8.8.0
