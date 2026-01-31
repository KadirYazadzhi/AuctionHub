# 🔨 AuctionHub

![Build Status](https://img.shields.io/badge/Build-Passing-success?style=for-the-badge&logo=appveyor)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)
![Status](https://img.shields.io/badge/Status-In%20Development-yellow?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)

> **Note:** This project is currently **Under Active Development** as part of the SoftUni ASP.NET Fundamentals course. Features are being rolled out incrementally.

## 📖 Overview

**AuctionHub** is a dynamic online bidding platform inspired by classic auction sites like eBay and BalkanAuction. It provides a competitive environment where users can list items for sale, place bids in real-time, and track their auctions.

The goal of this project is to demonstrate a robust implementation of a relational data model within an **ASP.NET Core MVC** architecture, focusing on data integrity, user concurrency, and a seamless user experience.

## 🚀 Key Features (Planned & Implemented)

### 👤 User Module
*   **Secure Authentication:** Powered by ASP.NET Core Identity.
*   **User Profiles:** Track reputation and history.
*   **Wallet System:** (Coming Soon) Virtual currency handling for secure bidding simulations.

### 🛒 Auction Management
*   **Create Listings:** Users can publish auctions with images, descriptions, and starting prices.
*   **Dynamic Status:** Auctions automatically transition from `Active` to `Sold` or `Expired` based on time.
*   **Categorization:** Items are organized into hierarchical categories for easy browsing.

### 💸 Bidding Engine
*   **Real-time Logic:** Strict validation ensures bids are always higher than the current price.
*   **Anti-Sniping:** (Planned) Logic to prevent unfair last-second bids.
*   **Bid History:** Transparent log of all activity on a listing.

## 🛠️ Tech Stack

| Component | Technology |
|Struture| **MVC** (Model-View-Controller) |
| Framework | **ASP.NET Core 8.0** |
| ORM | **Entity Framework Core** |
| Database | **Microsoft SQL Server** |
| UI Framework | **Bootstrap 5** & **Razor Views** |
| Validation | **FluentValidation** & **Data Annotations** |

## 📂 Project Structure

```text
AuctionHub/
├── Data/           # DbContext & Database Configurations
├── Models/         # Domain Entities (Auction, Bid, Category)
├── Controllers/    # Application Logic & Flow Control
├── Views/          # Razor Pages (UI)
└── wwwroot/        # Static Assets (CSS, JS, Images)
```

## 🔧 Getting Started

### Prerequisites
*   [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
*   [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (LocalDB or Full Instance)

### Installation

1.  **Clone the repository**
    ```bash
    git clone https://github.com/YourUsername/AuctionHub.git
    cd AuctionHub
    ```

2.  **Configure Database**
    Update the `ConnectionStrings` in `appsettings.json` to match your local SQL Server instance.

3.  **Apply Migrations**
    ```bash
    dotnet ef database update
    ```

4.  **Run the Application**
    ```bash
    dotnet run
    ```

## 🤝 Contributing

Contributions are welcome! Since this is an educational project, please open an issue first to discuss what you would like to change.

## 📜 License

Distributed under the MIT License. See `LICENSE` for more information.
