# Market Charts

A React application that visualizes market performance data using real-time API data from Alpha Vantage and StockData.org.

## Features

- Interactive market performance charts showing data for major market indices (S&P 500, Dow Jones, NASDAQ)
- Real-time data fetching from Alpha Vantage API with StockData.org as a fallback
- Server-side proxy to avoid exposing API keys in client-side code
- Caching system to minimize API calls and handle rate limits
- Data persistence using SQLite database on the server
- Configurable timeframes and events

## Setup Instructions

### Prerequisites

- Node.js (v14 or higher)
- npm (v7 or higher)

### Installation

1. Clone the repository
   ```bash
   git clone https://github.com/yourusername/market-charts.git
   cd market-charts
   ```

2. Install dependencies
   ```bash
   npm install
   ```

3. Configure API keys
   - Sign up for a free Alpha Vantage API key at [Alpha Vantage](https://www.alphavantage.co/support/#api-key)
   - Sign up for a free StockData.org API key at [StockData.org](https://www.stockdata.org/register)
   - Create a `.env` file in the root directory with your API keys:
     ```
     ALPHA_VANTAGE_API_KEY=your_alpha_vantage_api_key
     STOCKDATA_API_KEY=your_stockdata_api_key
     ```

### Running the Application

#### Development Mode

Run the server and client concurrently:
```bash
npm run dev
```

This will start:
- The React development server on port 3000 (http://localhost:3000)
- The Express API server on port 5001 (http://localhost:5001)

#### Production Mode

Build the React app and start the server:
```bash
npm run deploy
```

This will:
- Build the React app for production
- Start the Express server serving both the API and the built React app on port 5001

## API Architecture

The application uses a dual-API approach to handle rate limits and ensure data availability:

1. **Primary API**: Alpha Vantage
   - Used for fetching daily market data for major indices
   - Free tier limited to 25 requests per day (5 per minute)

2. **Secondary API (Fallback)**: StockData.org
   - Used as a fallback when Alpha Vantage rate limits are reached
   - Different rate limit structure (50 requests per day on free tier)

3. **Server-side Proxy**:
   - Hides API keys from client-side code
   - Transforms data between formats when needed
   - Provides unified API endpoints for the client

4. **Caching System**:
   - SQLite database for server-side persistence
   - In-memory cache for quick access
   - Browser localStorage as a fallback in client-only mode

## Configuration

All configuration options are available in `src/config.js`:

- **API Keys**: Replace placeholder API keys with your own
- **Market Symbols**: Configure which ETFs to use for market indices
- **Timeframes**: Configure date ranges for charts and key events
- **Simulation Mode**: Enable/disable simulation mode for development

## Data Flow

1. Client requests data from the server
2. Server checks for cached data in the database
3. If no cached data or cache is expired, server fetches from Alpha Vantage
4. If Alpha Vantage fails or hits rate limits, server tries StockData.org
5. Server transforms data to a consistent format and returns to client
6. Server also caches the results in the database for future requests
7. Client displays the data in charts and caches in localStorage as a fallback

## Rate Limits and Fallbacks

- **Alpha Vantage**: Free tier limited to 25 requests per day (5 per minute)
- **StockData.org**: Free tier limited to 50 requests per day
- **Caching**: Data is cached to minimize API calls
- **Fallback System**: Automatic fallback between APIs and cache layers

## License

This project is licensed under the MIT License - see the LICENSE file for details.
