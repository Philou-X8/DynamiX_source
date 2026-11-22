#pragma once

// Marks a printf-style function so GCC checks its format strings against the
// arguments. 'fmt' and 'args' are 1-based parameter positions; on a class
// member, 'this' counts as parameter 1. Expands to nothing on other compilers.
#if defined(__GNUC__)
#define PRINTF_FORMAT(fmt, args) __attribute__((format(printf, fmt, args)))
#else
#define PRINTF_FORMAT(fmt, args)
#endif
