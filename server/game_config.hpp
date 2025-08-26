#pragma once

struct GameConfig {
    static constexpr int MAX_EVENTS = 10;

    static constexpr float MAP_WIDTH = 100.0f;
    static constexpr float MAP_HEIGHT = 100.0f;
    static constexpr int INIT_FOOD_COUNT = 1000;
    //static constexpr int MAX_PLAYERS = 64;
    static constexpr float SPACE = 0.5f;

    static constexpr float BOOST_SPEED = 2.0f;
    static constexpr float BASIC_SPEED = 1.3f;
    static constexpr float BOOST_TICK = 1.0f;
    static constexpr float DELTA_TIME = 0.02f;
};
