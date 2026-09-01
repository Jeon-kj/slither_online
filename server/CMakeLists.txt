cmake_minimum_required(VERSION 3.10)
project(MyServer)

set(CMAKE_CXX_STANDARD 17)

add_executable(MyServer
    main.cpp
    message_handler.cpp
    message_sender.cpp
    server.cpp
    utils.cpp
    slither_manager.cpp
    food_manager.cpp
    collision_manager.cpp
)
