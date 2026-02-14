# 🔨 AuctionHub - Premium Digital Marketplace

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0-cyan?style=flat&logo=nuget)](https://docs.microsoft.com/en-us/ef/core/)
[![Bootstrap](https://img.shields.io/badge/Bootstrap-5.0-7952B3?style=flat&logo=bootstrap)](https://getbootstrap.com/)
[![License](https://img.shields.io/badge/License-MIT-green?style=flat)](LICENSE)
[![Status](https://img.shields.io/badge/Status-Completed-success?style=flat)]()

> **AuctionHub** is a robust, full-stack ASP.NET Core application designed to simulate a real-world auction environment. It features a complete financial ledger system, real-time bidding logic with concurrency protection, and a comprehensive administration dashboard.

<br />
<img src="./preview/home-preview.png" width="100%" alt="AuctionHub Home Page" />
<br />

---

## 📑 Table of Contents
1. [🌟 Key Features](#-key-features)
2. [📸 Gallery & UI](#-gallery--ui)
3. [⚙️ How It Works](#-how-it-works)
4. [🏗️ Technical Architecture](#-technical-architecture)
5. [💾 Database Schema](#-database-schema)
6. [📂 Project Structure](#-project-structure)
7. [🚀 Installation](#-installation--setup)
8. [🧪 Testing](#-testing)

---

## 🌟 Key Features

### 🛒 Auction System
* **Dynamic Listings:** Users can create auctions with start time, end time, and starting price.
* **Smart Bidding:**
    * **Validation:** Prevents bids lower than the current price.
    * **Self-Outbid Protection:** Users cannot bid on their own auctions.
    * **Concurrency Control:** Uses `RowVersion` to handle simultaneous bids seamlessly.
* **Buy It Now:** Option for immediate purchase functionality.
* **Watchlist:** Users can "star" items to track them without bidding.

### 💰 Financial Ecosystem (Wallet)
* **Internal Banking:** Every user has a digital wallet.
* **Escrow Service:** When a bid is placed, funds are **locked** (hold) immediately.
* **Auto-Refund:** If a user is outbid, their held funds are automatically released back to their available balance.
* **Transaction Ledger:** A persistent history of all Deposits, Withdrawals, Holds, and Releases.

### 🛡️ Administration Area
* **Dashboard:** Real-time metrics (Total Users, Active Auctions, Volume).
* **User Management:** Ability to view user details and history.
* **Moderation:** Admins can edit categories and suspend suspicious auctions.

### 🔔 User Engagement
* **Notifications:** Alert system for "Auction Won", "Outbid", or "Auction Ended".
* **Search & Filter:** Advanced filtering by Category, Price Range, and Status.

---

## 📸 Gallery & UI

### 1. User Experience (The Marketplace)

**Explore All Auctions**
*A clean grid view of all available items.*
<img src="./preview/explore-auctions-preview.png" width="100%" alt="Explore Auctions" />

**Advanced Filtering**
*Users can filter by specific categories and price ranges.*
<img src="./preview/explore-auctions-with-filter-preview.png" width="100%" alt="Explore Auctions Filtered" />

**Auction Details & Bidding**
*Detailed view showing current bid, bid history, and countdown timer.*
<img src="./preview/auction-details-preview.png" width="100%" alt="Auction Details" />

**My Wallet**
*The financial hub showing balance and transaction history.*
<img src="./preview/wallet-history-preview.png" width="100%" alt="Wallet History" />

---

### 2. Administration Area

**Admin Dashboard**
*Real-time statistics and platform overview.*
<img src="./preview/admin-panel-dashboard-preview.png" width="100%" alt="Admin Dashboard" />

**Admin Inbox**
*Internal communication and system notifications.*
<img src="./preview/admin-panel-inbox-preview.png" width="100%" alt="Admin Inbox" />

**User Management**
*View, edit, or ban users.*
<img src="./preview/admin-panel-users-preview.png" width="100%" alt="Admin Users" />

**Auction Management**
*Oversee all active and expired auctions.*
<img src="./preview/admin-panel-auctions-preview.png" width="100%" alt="Admin Auctions List" />

**Category Management**
*Create and edit product categories.*
<img src="./preview/admin-panel-categories-preview.png" width="100%" alt="Admin Categories" />

**Transaction Logs**
*Audit trail of all financial movements in the system.*
<img src="./preview/admin-panel-transaction-preview.png" width="100%" alt="Admin Transactions" />

---

## ⚙️ How It Works

1.  **Registration:** User creates an account via ASP.NET Identity.
2.  **Deposit:** User adds virtual funds to their Wallet via the "Deposit" action.
3.  **Bid:**
    * User places a bid on an item.
    * System checks `AvailableBalance`.
    * Funds are moved to `HeldBalance` (Escrow).
    * Previous highest bidder gets their funds refunded instantly.
4.  **Win:**
    * Auction timer expires (handled by `AuctionCleanupService`).
    * Winner's held funds are transferred to the Seller.
    * Ownership of the item is transferred.

---

## 🏗️ Technical Architecture

The solution uses a **Monolithic Architecture** with clear separation of concerns, following the **Service-Repository Pattern**.

* **Presentation Layer:** ASP.NET MVC (Controllers & Views).
* **Service Layer:** Business logic resides here (e.g., `AuctionService`, `WalletService`). This makes the code testable and reusable.
* **Data Layer:** Entity Framework Core with SQL Server.
* **Background Services:**
    * `AuctionCleanupService`: A hosted service (`IHostedService`) that runs in the background to automatically close expired auctions and process transfers.

### Tech Stack
| Component | Technology |
|-----------|------------|
| **Framework** | .NET 8.0 (C# 12) |
| **Web App** | ASP.NET Core MVC |
| **Database** | MS SQL Server 2019+ |
| **ORM** | Entity Framework Core (Code-First) |
| **Frontend** | Razor, Bootstrap 5, jQuery |
| **Testing** | xUnit, Moq, EF Core InMemory |

---

## 💾 Database Schema

The database relies on strong relationships to ensure data integrity.

```mermaid
erDiagram
    ApplicationUser ||--o{ Auction : "Creates"
    ApplicationUser ||--o{ Bid : "Places"
    ApplicationUser ||--o{ Transaction : "Has"
    ApplicationUser ||--o{ Notification : "Receives"
    
    Auction ||--o{ Bid : "Contains"
    Auction }|--|| Category : "In"
    
    Bid }|--|| ApplicationUser : "By"
    
    Transaction {
        string Type "Deposit/Withdraw/Hold"
        decimal Amount
        datetime Date
    }

```

---

## 📂 Project Structure

```text
AuctionHub/
├── Areas/
│   ├── Admin/              # Administration Controllers & Views
│   └── Identity/           # Auth Logic (Scaffolded)
├── Controllers/            # MVC Controllers (Web Layer)
├── Data/                   # DbContext & Seeding
├── Models/                 # Database Entities
│   └── ViewModels/         # DTOs for UI rendering
├── Services/               # Business Logic Layer (The Core)
│   ├── AuctionService.cs
│   └── WalletService.cs
├── Views/                  # Razor Pages
└── wwwroot/                # Static files (CSS, JS, Images)

```

---

## 🚀 Installation & Setup

To run this project locally, follow these steps:

1. **Prerequisites:**

* .NET 8.0 SDK
* SQL Server (LocalDB or full instance)

2. **Clone the Repo:**

```bash
git clone [https://github.com/YourUsername/AuctionHub.git](https://github.com/YourUsername/AuctionHub.git)

```

3. **Configure Connection:**

* Open `appsettings.json`.
* Modify `"DefaultConnection"` string if necessary.

4. **Database Migration:**

```bash
dotnet ef database update

```

*Note: The app includes a `DbSeeder` which will automatically create Categories and an Admin user.*
5. **Run:**

```bash
dotnet run

```

6. **Login Credentials (Seed Data):**

* **Admin:** `admin@auctionhub.com` / `admin123` (Check `DbSeeder.cs` to confirm)
* **User:** You can register a new user normally.

---

## 🧪 Testing

The project utilizes **xUnit** for unit testing, focusing on the Service Layer to ensure business logic validity.

* **Mocking:** `Moq` is used to simulate Database Context and repositories.
* **Coverage:** Covers Bidding logic, Validation, and Wallet calculations.

Run tests with:

```bash
dotnet test

```

---

*Project created for SoftUni ASP.NET Fundamentals Course.*
