#include "memory.h"
#include <Psapi.h>
#include <iostream>
#include <sstream>

#pragma comment(lib, "Psapi.lib")

#define INRANGE(x, a, b) (x >= a && x <= b)
#define getBits(x) (INRANGE((x & (~0x20)), 'A', 'F') ? ((x & (~0x20)) - 'A' + 0xa) : (INRANGE(x, '0', '9') ? x - '0' : 0))
#define get_byte(x) (getBits(x[0]) << 4 | getBits(x[1]))

namespace Memory {

    uintptr_t getBase() {
        return (uintptr_t)GetModuleHandleA(NULL);
    }

    uintptr_t FindPatternIDA(const char* szSignature) {
        MODULEINFO modInfo;
        GetModuleInformation(GetCurrentProcess(), GetModuleHandleA(NULL), &modInfo, sizeof(MODULEINFO));
        uintptr_t startAddress = (uintptr_t)GetModuleHandleA(NULL);
        uintptr_t endAddress = startAddress + (uintptr_t)modInfo.SizeOfImage;
        const char* pat = szSignature;
        uintptr_t firstMatch = 0;

        for (uintptr_t pCur = startAddress; pCur < endAddress; pCur++) {
            if (!*pat) return firstMatch;
            if (*(PBYTE)pat == '?' || *(BYTE*)pCur == get_byte(pat)) {
                if (!firstMatch) firstMatch = pCur;
                if (!pat[2]) return firstMatch;
                if (*(PWORD)pat == ('??') || *(PBYTE)pat != '?') pat += 3;
                else pat += 2;
            }
            else {
                pat = szSignature;
                firstMatch = 0;
            }
        }
        return 0;
    }

    void WriteLog(const char* name, uintptr_t addr) {
        std::stringstream ss;
        ss << std::hex << std::uppercase << addr;
        std::cout << "[+] " << name << " : 0x" << ss.str() << "\n";
    }
}
