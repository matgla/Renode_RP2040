#
# CMakeLists.txt
#
# Copyright (c) 2024 Mateusz Stadnik <matgla@live.com>
#
# Distributed under the terms of the MIT License.
#

add_executable(fifo_test)

target_sources(fifo_test PRIVATE test.cpp)
target_link_libraries(fifo_test PRIVATE piosim)
