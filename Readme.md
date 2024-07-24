# Shigen Senso Server

The Shigen Senso Server is the backend component of the Shigen Senso game, responsible for managing game state, handling client requests, and ensuring smooth gameplay. This repository contains the server-side code developed using C# and various supporting technologies.

## Overview

The Shigen Senso Server provides the necessary infrastructure to support a multiplayer strategy game. It manages player data, game sessions, resource generation, and combat mechanics. The server ensures that all players have a consistent and fair experience by maintaining authoritative control over the game state.

## Key Features

### Game State Management

- **Player Data**: Stores and manages data related to each player, including resources, structures, units, and progress.
- **Game Sessions**: Manages active game sessions, allowing players to join, leave, and interact within the game world.
- **Resource Generation**: Handles the generation and distribution of resources based on player activities and game events.

### Networking and Communication

- **Real-Time Communication**: Uses TCP/IP to provide real-time communication between the server and clients, ensuring low-latency updates and interactions.

### Combat Mechanics

- **Battle Management**: Handles combat scenarios between players, including attack resolution, defense calculations, and outcome determination.
- **Unit Management**: Manages the creation, movement, and actions of units within the game world.

### Pathfinding and Map Generation

- **Pathfinding**: Implements pathfinding algorithms to ensure that units can navigate the hexagonal grid map effectively.
- **Map Generation**: Supports dynamic generation of the game map, ensuring a unique and balanced layout for each game session.

## Game Demo
- https://youtu.be/0ALPqSQx0po

## Images 

### Hexagon Types
![Free Land](images/hex_freeland.png)
![Mountain](images/hex_mountian.png)
![Forest](images/hex_forest.png)
![Crops](images/hex_crops.png)

### Map
![Map](images/map_complete.png)

### Structures
![Stone Mine](images/struct_stoneMine.png)
![Sawmill](images/struct_sawmill.png)
![Farm](images/struct_farm.png)

### Resource Collection
![Stone Mine x2](images/struct_stoneMine_2x.png)
![Stone Mine x3](images/struct_stoneMine_3x.png)

### UI
![Login](images/ui_login.png)
![Main Menu](images/ui_mainManu.png)
![In Game UI](images/ui_player2.png)
![Not Enough Resources](images/ui_no_resources.png)
![Successfull Build](images/ui_yes_resources.png)

### Pathfinding
![Pathfinding](images/ui_unit_pathdinding.png)
