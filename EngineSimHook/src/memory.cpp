#include "memory.h"
#include <Psapi.h>
#include <vector>

#pragma comment(lib, "Psapi.lib")

#define INRANGE(x, a, b) (x >= a && x <= b)
#define getBits(x) (INRANGE((x & (~0x20)), 'A', 'F') ? ((x & (~0x20)) - 'A' + 0xa) : (INRANGE(x, '0', '9') ? x - '0' : 0))
#define get_byte(x) (getBits(x[0]) << 4 | getBits(x[1]))

namespace Memory {

    uintptr_t getBase() {
        return (uintptr_t)GetModuleHandleA(NULL);
    }

    uintptr_t FindPatternIDA(const char* szSignature) {
        auto all = FindPatternAll(szSignature);
        return all.empty() ? 0 : all[0];
    }

    std::vector<uintptr_t> FindPatternAll(const char* szSignature) {
        std::vector<uintptr_t> matches;
        MODULEINFO modInfo;
        GetModuleInformation(GetCurrentProcess(), GetModuleHandleA(NULL), &modInfo, sizeof(MODULEINFO));
        uintptr_t startAddress = (uintptr_t)GetModuleHandleA(NULL);
        uintptr_t endAddress = startAddress + (uintptr_t)modInfo.SizeOfImage;
        const char* pat = szSignature;
        uintptr_t firstMatch = 0;

        for (uintptr_t pCur = startAddress; pCur < endAddress; pCur++) {
            if (!*pat) {
                // Full pattern matched
                matches.push_back(firstMatch);
                pat = szSignature;
                firstMatch = 0;
                continue;
            }
            if (*(PBYTE)pat == '?' || *(BYTE*)pCur == get_byte(pat)) {
                if (!firstMatch) firstMatch = pCur;
                if (!pat[2]) {
                    // Last byte of pattern matched
                    matches.push_back(firstMatch);
                    pat = szSignature;
                    firstMatch = 0;
                    continue;
                }
                if (*(PWORD)pat == ('??') || *(PBYTE)pat != '?') pat += 3;
                else pat += 2;
            }
            else {
                pat = szSignature;
                firstMatch = 0;
            }
        }
        return matches;
    }
}
