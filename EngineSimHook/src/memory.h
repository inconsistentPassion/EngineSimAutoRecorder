#pragma once
#ifndef MEMORY_H
#define MEMORY_H

#include <Windows.h>
#include <cstdint>
#include <vector>

namespace Memory {
    uintptr_t getBase();
    uintptr_t FindPatternIDA(const char* szSignature);
    std::vector<uintptr_t> FindPatternAll(const char* szSignature);
}

#endif
