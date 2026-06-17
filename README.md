# Disaster Coordination System - Volunteer Management Module

A centralized emergency response platform designed to streamline volunteer operations, skills tracking, and resource mobilization during natural disasters and crises. This repository contains the end-to-end **Volunteer Management Module**, built using a modern serverless cloud architecture.

## 🚀 Features# Disaster Coordination System

A centralized emergency response and crisis management platform designed to streamline volunteer operations, real-time incident tracking, resource distribution, and task mobilization during natural disasters. This platform bridges the gap between relief organizations, field teams, and community volunteers to ensure rapid and targeted aid deployment.

## 🚀 Core Modules

### 1. Volunteer Management Module (My Contribution)
- **Secure Authentication:** User registration, multi-factor login, and identity management powered by AWS Cognito.
- **Dynamic Profiles:** Tracks contact info, real-time availability status (`ACTIVE`/`INACTIVE`), and regional dependency data mapped to Sri Lankan districts and cities.
- **Skills Matrix:** A robust skill-tracking system (First Aid, Search & Rescue, Logistics, Driving, etc.) to immediately identify qualified personnel during an emergency dispatch.
- **Optimized Cloud Storage:** Handles profile images by streaming them directly to Amazon S3, ensuring the database stays lightweight.

### 2. Incident Reporting & Tracking
- **Real-Time Mapping:** Allows field agents and citizens to report ongoing disaster incidents (floods, landslides, fires) with exact location tags and severity levels.
- **Live Dashboard:** Provides administrators with a visual overview of active crises to prioritize response efforts.

### 3. Resource Allocation & Supply Chain
- **Inventory Control:** Tracks emergency relief items such as food packages, water, medical supplies, and heavy machinery.
- **Smart Routing:** Ensures resources are dispatched to high-severity areas efficiently without duplication or supply chain waste.

### 4. Task Assignment & Dispatch
- **Incident-to-Volunteer Matching:** Enables coordinators to create emergency tasks and instantly link them with available local volunteers based on required skills.
- **Progress Tracking:** Monitors task statuses from "Dispatched" to "Completed" dynamically.

---

## 🛠️ Tech Stack

### Frontend
- **Framework:** Next.js (React)
- **Language:** TypeScript / JavaScript
- **Styling:** Tailwind CSS
- **Hosting:** AWS Amplify

### Cloud Infrastructure & Security
- **Identity & Auth:** AWS Cognito
- **Object Storage:** Amazon S3 (Simple Storage Service)

### Backend & Database
- **Backend Core:** C# (.NET Core Web API)
- **Database:** Amazon DynamoDB (NoSQL Data Store optimized with Single-Table Design)

---

## 🏗️ Architecture Overview

The system architecture is built to be highly scalable, serverless, and fault-tolerant:
1. **Client Layer:** Next.js delivers a responsive, fast web interface hosted globally on AWS Amplify.
2. **Security Layer:** AWS Cognito safeguards endpoints, ensuring strict role-based data isolation.
3. **Storage Pipeline:** Heavy media files (profile images/incident photos) bypass backend computing and stream directly to Amazon S3. The database only retains the lightweight object URLs.
4. **Data & Compute Layer:** The C# API acts as the engine, executing business logic and reading/writing to Amazon DynamoDB with sub-millisecond latency.

---

## 💻 Getting Started

### Prerequisites
- Node.js (v18 or higher)
- .NET SDK (v8.0 or higher)
- AWS CLI configured with appropriate permissions

### Frontend Setup (Next.js)
```bash
# Clone the repository
git clone [https://github.com/harinins2001-create/Disaster-Coordination.git](https://github.com/harinins2001-create/Disaster-Coordination.git)

# Navigate to frontend directory
cd client

# Install dependencies
npm install

# Run development server
npm run dev

Backend Setup (C#)

# Navigate to backend directory
cd server

# Restore NuGet dependencies
dotnet restore

# Run the project
dotnet run
