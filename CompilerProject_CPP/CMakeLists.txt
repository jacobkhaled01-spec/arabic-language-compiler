cmake_minimum_required(VERSION 3.20)
project(CompilerProject_CPP LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include_directories(include)

file(GLOB_RECURSE SOURCES "src/*.cpp")

add_executable(CompilerProject_CPP ${SOURCES})

if (MSVC)
    target_compile_options(CompilerProject_CPP PRIVATE /utf-8 /EHsc /O2)
else()
    target_compile_options(CompilerProject_CPP PRIVATE -O2 -Wall)
endif()
