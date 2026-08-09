#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <cstdio>
#include <thread>
#include <chrono>

#include "MinHook/include/MinHook.h"

#define LOG(fmt, ...) printf("[hideSRuid] " fmt "\n", ##__VA_ARGS__)
#define ERR(fmt, ...) printf("[hideSRuid::Error] " fmt "\n", ##__VA_ARGS__)
