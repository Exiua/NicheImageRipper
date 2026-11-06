# Builds megasdk, a shared library that exposes the MEGA SDK through the flat C ABI in include/megasdk_c.h.
#
# The SDK is pulled in with add_subdirectory and linked statically, so the output is a single library that goes in
# ../src/MegaSdk/runtimes/<rid>/native/. See ../README.md for build instructions.

cmake_minimum_required(VERSION 3.20)

set(MEGA_SDK_DIR "${CMAKE_CURRENT_LIST_DIR}/sdk" CACHE PATH "Path to a meganz/sdk checkout (the sdk submodule by default)")
get_filename_component(MEGA_SDK_DIR "${MEGA_SDK_DIR}" ABSOLUTE)

if(NOT EXISTS "${MEGA_SDK_DIR}/include/megaapi.h")
    message(FATAL_ERROR "MEGA_SDK_DIR (${MEGA_SDK_DIR}) is not a meganz/sdk checkout. "
                        "Run: git submodule update --init lib/MegaSdk/native/sdk")
endif()

set(VCPKG_ROOT "" CACHE PATH "If set, it will build and use the VCPKG packages defined in the SDK's manifest file")

list(APPEND CMAKE_MODULE_PATH ${MEGA_SDK_DIR}/cmake/modules)

# Only the core client is needed for browsing and downloading. Turning these off also keeps vcpkg from building
# FreeImage, FFmpeg, PDFium, MediaInfo and libuv.
set(ENABLE_SYNC OFF CACHE BOOL "")
set(ENABLE_CHAT OFF CACHE BOOL "")
set(ENABLE_ISOLATED_GFX OFF CACHE BOOL "")
set(ENABLE_MEDIA_FILE_METADATA OFF CACHE BOOL "")
set(USE_FREEIMAGE OFF CACHE BOOL "")
set(USE_FFMPEG OFF CACHE BOOL "")
set(USE_PDFIUM OFF CACHE BOOL "")
set(USE_LIBUV OFF CACHE BOOL "")
set(USE_READLINE OFF CACHE BOOL "")
set(ENABLE_SDKLIB_EXAMPLES OFF CACHE BOOL "")
set(ENABLE_SDKLIB_TESTS OFF CACHE BOOL "")
set(ENABLE_SDKLIB_WERROR OFF CACHE BOOL "")

include(sdklib_options)

if(VCPKG_ROOT)
    # ICU is built with autotools, which refuse to run from (or mis-parse) paths with these characters.
    # Packages are installed into <build dir>/vcpkg_installed, so the build directory is affected too.
    foreach(dir IN ITEMS VCPKG_ROOT CMAKE_BINARY_DIR)
        if(${dir} MATCHES "[# $&'\"]")
            message(FATAL_ERROR "${dir} (${${dir}}) contains a character autotools can't handle (e.g. '#' or a "
                                "space). Use a plain path such as C:/dev/vcpkg or C:/dev/build-megasdk.")
        endif()
    endforeach()

    include(vcpkg_management)
    # Our overlay triplets come first so they take precedence over the SDK's (e.g. its Windows triplets pin the
    # VS 2019 toolset).
    process_vcpkg_libraries("${CMAKE_CURRENT_LIST_DIR}/cmake;${MEGA_SDK_DIR}/cmake")
    set(VCPKG_MANIFEST_DIR "${MEGA_SDK_DIR}")
else()
    list(APPEND CMAKE_MODULE_PATH ${MEGA_SDK_DIR}/cmake/modules/packages)
    message(STATUS "Using system dependencies")
endif()

project(megasdk_c
    VERSION 1.0.0
    DESCRIPTION "C ABI over the MEGA SDK"
    LANGUAGES C CXX
)

# The static SDK is linked into a shared library.
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

add_subdirectory(${MEGA_SDK_DIR} ${CMAKE_BINARY_DIR}/sdk EXCLUDE_FROM_ALL)

add_library(megasdk SHARED src/megasdk_c.cpp)

target_include_directories(megasdk PUBLIC ${CMAKE_CURRENT_LIST_DIR}/include)
target_link_libraries(megasdk PRIVATE MEGA::SDKlib)
target_compile_features(megasdk PRIVATE cxx_std_17)

set_target_properties(megasdk PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    # Same file name as on Windows so the C# side can use one library name.
    PREFIX ""
)

if(MSVC)
    target_compile_options(megasdk PRIVATE /bigobj)
endif()

# Copy the library next to the C# project, where it's packed and copied to the output.
if(NOT MEGASDK_RID)
    if(WIN32)
        set(MEGASDK_RID_OS "win")
    elseif(APPLE)
        set(MEGASDK_RID_OS "osx")
    else()
        set(MEGASDK_RID_OS "linux")
    endif()

    if(CMAKE_GENERATOR_PLATFORM MATCHES "ARM64" OR CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64|ARM64")
        set(MEGASDK_RID_ARCH "arm64")
    elseif(CMAKE_GENERATOR_PLATFORM MATCHES "Win32")
        set(MEGASDK_RID_ARCH "x86")
    else()
        set(MEGASDK_RID_ARCH "x64")
    endif()

    set(MEGASDK_RID "${MEGASDK_RID_OS}-${MEGASDK_RID_ARCH}")
endif()

set(MEGASDK_RUNTIME_DIR "${CMAKE_CURRENT_LIST_DIR}/../src/MegaSdk/runtimes/${MEGASDK_RID}/native"
    CACHE PATH "Where the built library is copied")

add_custom_command(TARGET megasdk POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${MEGASDK_RUNTIME_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy_if_different "$<TARGET_FILE:megasdk>" "${MEGASDK_RUNTIME_DIR}"
    COMMENT "Copying megasdk to ${MEGASDK_RUNTIME_DIR}"
)
