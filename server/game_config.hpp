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

    // 서버 충돌 활성화 여부
    static constexpr bool ENABLE_COLLISION = true;
    // 머리 반경
    static constexpr float HEAD_RADIUS = 0.25f;
    // 몸통 반경
    static constexpr float BODY_RADIUS = 0.25f;
    // 먹이 반경
    static constexpr float FOOD_RADIUS = 0.1f;
    // 스폰 충돌 면역 시간
    static constexpr float SPAWN_IMMUNITY_TIME = 1.0f;
    // 맵 경계에서 확보할 스폰 여백
    static constexpr float SPAWN_MARGIN = 5.0f;
};
