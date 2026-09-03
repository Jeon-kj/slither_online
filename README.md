# Slither Online

Unity 클라이언트와 Linux C++ 서버로 구현한 Slither 스타일의 온라인 멀티플레이 게임 프로토타입입니다.

게임 서버 프로그래밍을 공부하기 위해 제작했으며,  
TCP 소켓 통신과 `epoll` 기반 네트워크 처리, 서버 게임 루프를 직접 구현하는 데 초점을 맞췄습니다.

## Demo

[YouTube](https://youtu.be/hWauhAuebzQ)

## Tech Stack

### Client
- Unity
- C#
- TCP

### Server
- C++17
- Linux
- TCP Socket
- epoll
- CMake
- JSON

## Architecture

```text
Unity Client
    │
    │ TCP
    ▼
C++ Game Server (Linux)

├─ Network Loop
│  ├─ epoll
│  ├─ client connection
│  ├─ recv / send
│  └─ packet parsing
│
└─ Game Loop
   ├─ movement
   ├─ boost
   ├─ collision
   ├─ food
   └─ player state update
```

서버는 **Network Loop와 Game Loop를 별도의 스레드로 실행**합니다.

Network Loop에서는 non-blocking socket과 `epoll`을 이용해 여러 클라이언트의 연결과 송수신을 처리하고,  
Game Loop에서는 일정 주기로 플레이어 이동, 부스트, 충돌, 먹이 생성 및 상태 갱신을 처리합니다.

## Network

TCP 위에 간단한 메시지 프로토콜을 구현했습니다.

```text
[Message Type][Body Length][JSON Body]
```

- Message Type: 4 bytes
- Body Length: 4 bytes
- Body: UTF-8 JSON

수신 버퍼를 이용해 TCP stream에서 완전한 메시지를 복원하고,  
송신 시에도 send queue를 통해 partial send를 처리합니다.

## Message Types

다음과 같은 게임 데이터를 주고받습니다.

- Player Join / Leave
- Player Position
- Player Angle
- Boost Status
- Chat
- Food Spawn / Remove
- Slither Length Update

초기에는 Hit / Eat 결과를 클라이언트에서 서버로 전달하는 방식도 구현했지만,  
현재는 충돌과 먹이 획득을 서버에서 직접 판정하도록 변경했습니다.

## Server-side Game Logic

클라이언트는 주로 **방향과 부스트 입력**을 서버에 전달하고,  
서버가 실제 게임 상태를 계산합니다.

서버에서 처리하는 주요 로직은 다음과 같습니다.

- 플레이어 이동
- 부스트 및 길이 감소
- Slither 간 충돌 판정
- 먹이 생성 및 획득
- 성장 및 사망 처리
- 플레이어 상태 동기화

## Project Structure

```text
server/
├── server.*
│   └── Network Loop / Game Loop
├── message_handler.*
│   └── packet parsing / dispatch
├── message_sender.*
│   └── packet send / broadcast
├── slither_manager.*
│   └── movement / growth / death
├── collision_manager.*
│   └── collision detection
└── food_manager.*
    └── food state

client_unity/
└── Assets/Script/
    ├── NetworkManager.cs
    ├── MessageHandler.cs
    ├── SlitherManager.cs
    └── FoodManager.cs
```

## What I Learned

- Linux TCP Socket Programming
- non-blocking I/O와 `epoll`
- TCP packet framing
- partial recv / send 처리
- Network Loop와 Game Loop 분리
- 멀티스레드 공유 상태 동기화
- 서버 기반 게임 상태 처리

완성도 높은 게임 제작보다  
**온라인 게임 서버의 기본 구조와 네트워크 흐름을 직접 구현하며 이해하는 것**을 목표로 한 프로젝트입니다.
