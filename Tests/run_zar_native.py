"""Build and run the real AnyPS5 archive regressions on Linux without a full GPU build.

Requires a pinned AnyPS5 checkout with Source/anyps5 applied, GCC 13 or newer,
ZArchive sources, zstd headers and the system libzstd shared library.
Use the normal CMake/CTest build on Windows instead.
"""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
LIBC_SOURCES = (
    "General", "AppArchive", "ArchiveStdio", "ArchiveScanning", "FileAndHeap",
    "Formatting", "StandardStreams", "StreamPurge", "GuestArena", "GuestWriteWatch",
    "HostErrno", "HostEntropy", "RuntimeSupport", "Errors", "Process",
    "Directory", "GuestAllocations", "ScanningWide", "Multibyte", "RuneLocale",
)
KERNEL_FILE_SOURCES = (
    "Open", "Stdio", "ArchivedFile", "RandomDevice", "DirectoryDescriptor",
    "HostStreams", "NativeStat", "DescriptorControl", "SharedMemory",
)
OTHER_SOURCES = (
    "prx/libc/src/exception/Runtime.cpp",
    "prx/libSceLibcInternal/Directory.cpp",
    "prx/libkernel/DirectMemory/PosixMemory.cpp",
    "prx/libkernel/DirectMemory/DirectMemory.cpp",
    "prx/libkernel/DirectMemory/Export.cpp",
    "prx/libkernel/Aio/src/Aio.cpp",
    "prx/libkernel/DirectMemory/Jit.cpp",
    "prx/libkernel/Socket/src/Socket.cpp",
    "prx/libkernel/Socket/src/Udp.cpp",
    "prx/libkernel/Equeue/Equeue.cpp",
    "prx/libkernel/Equeue/src/FileEvent.cpp",
    "prx/libkernel/Equeue/src/Kqueue.cpp",
    "prx/libkernel/Time/TimedWait.cpp",
)
SUITES = (
    ("GuestAppArchive", ("present", "absent", "damaged", "invalid", "wrapped")),
    ("GuestArchiveStdio", ("plain", "archive", "damaged")),
    # The mmap refusal test relies on the guest terminate bridge in the full runtime.
    ("GuestKernelAppArchive", ("plain", "archive")),
)


def run(command):
    subprocess.run([str(part) for part in command], check=True)


def compiler(variable, preferred, fallback):
    requested = os.environ.get(variable)
    found = shutil.which(requested or preferred) or (None if requested else shutil.which(fallback))
    if not found:
        raise RuntimeError(f"Missing {variable} compiler: {requested or preferred}")
    return found


def build_and_test(args):
    source = args.anyps5_source.resolve()
    zarchive = (args.zarchive_source or source / "3rdparty/ZArchive").resolve()
    work = args.work_dir.resolve()
    libs = source / "core/libs"
    common = [libs / f"prx/libc/src/{name}.cpp" for name in LIBC_SOURCES]
    common += [libs / f"prx/libkernel/File/src/{name}.cpp" for name in KERNEL_FILE_SOURCES]
    common += [libs / name for name in OTHER_SOURCES]
    common += [zarchive / "src/zarchivereader.cpp", zarchive / "src/zarchivewriter.cpp"]
    tests = [libs / f"tests/{name}.cpp" for name, _ in SUITES]
    sha = zarchive / "src/sha_256.c"
    includes = [libs, libs / "nid/include", zarchive / "include", source / "3rdparty/zstd/lib"]
    required = common + tests + [sha, zarchive / "include/zarchive/zarchivereader.h",
                                  includes[-1] / "zstd.h"]
    missing = [str(path) for path in required if not path.is_file()]
    if missing:
        raise RuntimeError("Missing patched sources/dependencies:\n" + "\n".join(missing))

    cc = compiler("CC", "gcc-13", "gcc")
    cxx = compiler("CXX", "g++-13", "g++")
    work.mkdir(parents=True, exist_ok=True)
    flags = ["-std=c++20", "-O2", "-g", "-ffunction-sections", "-fdata-sections", "-pthread"]
    for path in includes:
        flags += ["-I", str(path)]
    objects = []
    for index, path in enumerate(common):
        obj = work / f"source-{index}.o"
        print(f"Compile {path.name}", flush=True)
        run([cxx, *flags, "-c", path, "-o", obj])
        objects.append(obj)
    sha_obj = work / "sha_256.o"
    run([cc, "-std=c11", "-O1", "-g", "-ffunction-sections", "-fdata-sections",
         "-c", sha, "-o", sha_obj])
    objects.append(sha_obj)

    passed = 0
    for name, modes in SUITES:
        test_obj = work / f"{name}.o"
        binary = work / name
        run([cxx, *flags, "-c", libs / f"tests/{name}.cpp", "-o", test_obj])
        run([cxx, "-pthread", "-Wl,--gc-sections", *objects, test_obj,
             f"-l:{args.zstd_library}", "-o", binary])
        for mode in modes:
            print(f"Run {name}: {mode}", flush=True)
            run([binary, mode])
            passed += 1
    print(f"PASS: {passed} real-backend regression modes ({zarchive})", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--anyps5-source", type=Path, default=ROOT / "Tools/AnyPS5")
    parser.add_argument("--zarchive-source", type=Path,
                        help="alternative ZArchive checkout, such as ../ZArchive-master")
    parser.add_argument("--work-dir", type=Path, default=ROOT / "work/zar-native")
    parser.add_argument("--zstd-library", default="libzstd.so.1",
                        help="system zstd library filename for the linker")
    args = parser.parse_args()
    if not sys.platform.startswith("linux"):
        parser.error("This focused runner targets Linux; use the CMake/CTest build on Windows.")
    try:
        build_and_test(args)
    except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
