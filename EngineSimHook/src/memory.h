#pragma once
#ifndef MEMORY_H
#define MEMORY_H

#include <Windows.h>
#include <cstdint>
#include <vector>

namespace Memory {
    uintptr_t getBase();
    uintptr_t FindPatternIDA(const char* szSignature);

    // Returns ALL matches for a pattern (for validation)
    std::vector<uintptr_t> FindPatternAll(const char* szSignature);

    // Validate that addr points to a real function (checks common x64 prolog bytes)
    bool ValidateFunctionProlog(uintptr_t addr);

    void WriteLog(const char* name, uintptr_t addr);
}

#endif
