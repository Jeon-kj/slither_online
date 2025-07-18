#pragma once
#include <iostream>
#include <chrono>
#include <iomanip>
#include <ctime>

inline bool debug_enabled = true;

template<typename... Args>
void dbg(Args&&... args) {
    if (!debug_enabled) return;

    // 현재 시간 가져오기
    auto now = std::chrono::system_clock::now();
    std::time_t now_time_t = std::chrono::system_clock::to_time_t(now);
    std::tm now_tm = *std::localtime(&now_time_t);

    // 시간 출력
    std::cout << "[" << std::put_time(&now_tm, "%Y-%m-%d %H:%M:%S") << "] ";

    // 실제 디버그 메시지 출력
    (std::cout << ... << args) << std::endl;
}
