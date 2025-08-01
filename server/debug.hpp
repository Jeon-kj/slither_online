#pragma once
#include <iostream>
#include <chrono>
#include <iomanip>
#include <ctime>
#include <set>

inline bool debug_enabled = true;
// LOCK, SLITHER_MOVE, ANGLE, SEQUENCE, HIT, EAT, JOIN, FOOD, FOOD_REMOVE, EAT
inline const std::set<std::string> excluded_tags = {
    "SLITHER_MOVE",
    "SEQUENCE",
    "LOCK",
    "ANGLE"
};

template<typename... Args>
void dbg_tag(const std::string& tag, Args&&... args) {
    if (!debug_enabled) return;
    if (excluded_tags.find(tag) != excluded_tags.end()) return;

    // 현재 시간
    auto now = std::chrono::system_clock::now();
    std::time_t now_time_t = std::chrono::system_clock::to_time_t(now);
    std::tm now_tm = *std::localtime(&now_time_t);

    std::cout << "[" << std::put_time(&now_tm, "%Y-%m-%d %H:%M:%S") << "] "
              << "[" << tag << "] ";

    (std::cout << ... << args) << std::endl;
}

template<typename... Args>
void dbg(Args&&... args) {
    dbg_tag("General", std::forward<Args>(args)...);
}