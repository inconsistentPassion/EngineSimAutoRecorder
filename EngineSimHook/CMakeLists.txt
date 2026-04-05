cmake_minimum_required(VERSION 3.20)
project(EngineSimHook LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ── MinHook (vendored) ────────────────────────────────────────────────

add_library(minhook STATIC
    vendor/minhook/src/buffer.c
    vendor/minhook/src/hook.c
    vendor/minhook/src/trampoline.c
    vendor/minhook/src/hde/hde32.c
    vendor/minhook/src/hde/hde64.c
)

target_include_directories(minhook PUBLIC vendor/minhook/include)

# ── Hook DLL ──────────────────────────────────────────────────────────

add_library(es_hook SHARED
    src/dllmain.cpp
    src/hooks.cpp
    src/memory.cpp
    src/pipe.cpp
)

target_include_directories(es_hook PRIVATE src)
target_link_libraries(es_hook PRIVATE minhook Psapi)

# Output to a predictable location
set_target_properties(es_hook PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/bin"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_SOURCE_DIR}/bin"
)
