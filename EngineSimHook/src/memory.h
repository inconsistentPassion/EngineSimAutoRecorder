#pragma once
#ifndef MEMORY_H
#define MEMORY_H

#include <Windows.h>
#include <cstdint>

namespace Memory {
    uintptr_t getBase();
    uintptr_t FindPatternIDA(const char* szSignature);
    void WriteLog(const char* name, uintptr_t addr);
}

#endif
