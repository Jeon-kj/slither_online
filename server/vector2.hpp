#pragma once
#include <cmath>

struct Vector2 {
    float x;
    float y;

    Vector2(float x_ = 0, float y_ = 0) : x(x_), y(y_) {}

    // 벡터 뺄셈 연산자
    Vector2 operator-(const Vector2& other) const {
        return Vector2(x - other.x, y - other.y);
    }

    // 길이 (magnitude)
    float magnitude() const {
        return std::sqrt(x * x + y * y);
    }

    // 거리 계산
    float distance_to(const Vector2& other) const {
        return (*this - other).magnitude();
    }

    bool operator==(const Vector2& other) const {
        return x == other.x && y == other.y;
    }
};