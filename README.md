# Valorant Discord Match Tracker

A Discord-integrated service that tracks Valorant matches, aggregates player statistics, and automatically posts match results and daily summaries across multiple Discord servers.

---

## Table of Contents
- [Overview](#overview)
- [Problem Statement](#problem-statement)
- [Features](#features)
- [Architecture](#architecture)
- [Technical Challenges](#technical-challenges)
- [Scale Assumptions](#scale-assumptions)
- [Tech Stack](#tech-stack)
- [Future Improvements](#future-improvements)
- [Notes](#notes)

---

## Overview

This is a personal, portfolio-focused backend service that integrates with Discord to automatically track and summarize Valorant post-match statistics across multiple Discord servers. Its goal is to help small gaming communities stay engaged by sharing match results and daily stat summaries, even when members aren’t playing together directly.

The service runs 24/7 as a long-lived background system, deployed on a Raspberry Pi with a separate Linux server hosting the database. It polls external Valorant APIs on a fixed schedule, ingests and deduplicates match data, persists historical stats, and posts structured summaries back to Discord. While currently used by a small group of real users, the system is designed with production-style concerns in mind.

A central constraint is the strict rate limiting of the external API (90 requests per minute). Polling, scheduling, and data ingestion are deliberately designed to stay within these limits while leaving room for on-demand queries, influencing both system architecture and scaling assumptions.

---

## Problem Statement

Discord communities playing Valorant often lack an automated way to track match results and player stats, relying instead on manual lookups or screenshots. This leads to inconsistent data and lower engagement across servers. Building a reliable tracking system is challenging due to strict API rate limits, occasional downtime, and the need to store and deduplicate match data. Supporting multiple Discord servers adds additional complexity around scheduling and polling. This project solves these problems by providing a long-running, Discord-integrated service that ingests match data, aggregates stats, and posts consistent summaries automatically.

---

## Features

- Discord bot integration
- Discord commands
- Automatic Valorant match polling
- Match result posting
- Daily / aggregate stat summaries
- Persistent data storage

---

## Architecture

High-level system design and data flow.

> Include an architecture diagram here.



**Components:**
- Discord Bot
- Background polling service
- Henrik API
- SQL Server (Dockerized)

---

## Technical Challenges

API rate limiting: The Henrik Valorant API allows 90 requests per minute, requiring careful scheduling and throttling to poll multiple users while leaving room for ad-hoc queries.

Data ingestion & deduplication: Match data must be stored reliably and checked for duplicates to ensure accurate historical stats.

Long-running background service: Maintaining a 24/7 service on a Raspberry Pi and a separate Linux server required robust error handling, retries, and logging.

Multi-server support: Polling, data storage, and summary posting needed to work seamlessly across multiple Discord servers without conflicts.

Handling external downtime: The system gracefully handles API failures or partial outages, ensuring the service continues running and catches up when data becomes available.

---

## Scale Assumptions

Discord servers: The service supports an unlimited number of servers; scaling is constrained primarily by the number of users being tracked rather than server count.

Users & polling: Polling is rate-limited by the Henrik API to ~15 requests per minute, with room to increase to 30 requests per minute. Beyond this, the system would be hard-limited by API restrictions.

Polling latency: With the current load (~30 users), any match is typically polled and recorded within 2 minutes. Doubling the user count (~60 users) would increase the maximum delay to ~4 minutes, and further increases would proportionally raise latency.

Scaling bottlenecks: Increasing users or polling frequency would require either multiple polling instances, a caching layer, or batch requests to stay within API limits while maintaining timely updates.

---

## Tech Stack

Language: C#

Discord Integration: Discord.Net

Testing / Mocks: Moq

Logging: Serilog

Databases: SQLite3 (lightweight local storage), SQL Server (Dockerized, for performance-critical storage)

Containerization: Docker (for SQL Server)

Hosting / Deployment: Raspberry Pi (polling service) + separate Linux server (SQL Server)

External APIs: Henrik Valorant API

---

## Future Improvements

Ideas for future enhancements.

- Improved stat analysis
- Web dashboard
- Caching layer
- Horizontal scaling
- Improved fault tolerance

---

## Notes

I definitely learned a lot with this. From database table design choices to setting up a service from scratch, it was a fun ride.
The community we've built around Valorant and this match tracker app really fueled me to work hard on it and continuously improve it.
The hype around Valorant has definitely died down for our group, but this match tracker will (hopefully) remain in the hearts of many.

While this readme was majorily helped by AI, this repository was mainly coded from scratch without the help of AI (can see from when commits were made).

---

