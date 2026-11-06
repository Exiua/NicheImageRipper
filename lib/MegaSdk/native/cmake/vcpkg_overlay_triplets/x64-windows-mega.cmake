# Same as the SDK's x64-windows-mega triplet, but with the VS 2022 toolset instead of the pinned VS 2019 one.

set(VCPKG_PLATFORM_TOOLSET v143)

set(VCPKG_TARGET_ARCHITECTURE x64)

# use dynamic C and CPP libraries
set(VCPKG_CRT_LINKAGE dynamic)

if(PORT MATCHES "ffmpeg")
    # build this library as DLL (usually because it is LGPL licensed)
    set(VCPKG_LIBRARY_LINKAGE dynamic)
else()
    # build this library statically, so megasdk.dll has no other DLL dependencies
    set(VCPKG_LIBRARY_LINKAGE static)
endif()
