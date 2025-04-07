// Alpha Vantage API configuration
export const ALPHA_VANTAGE_API_KEY = "RYXKW2Z10T4W29VW"; 

// Market symbols (standard symbols for major indices)
export const MARKET_SYMBOLS = {
  SP500: "SPY",     // S&P 500 ETF Trust (easier to use than ^GSPC)
  DOW: "DIA",       // SPDR Dow Jones Industrial Average ETF (easier to use than ^DJI)
  NASDAQ: "QQQ"     // Invesco QQQ Trust (tracks NASDAQ-100, easier to use than ^IXIC)
  
  // Note: Using ETFs instead of index symbols as they're more reliable with some APIs
  // Original index symbols: ^GSPC (S&P 500), ^DJI (Dow), ^IXIC (NASDAQ)
};

// Time frame configuration (for simulation purposes)
export const TIMEFRAMES = {
  // Trump's inauguration date (second term)
  INAUGURATION_DATE: "2025-01-20",
  
  // Tariff announcement date
  TARIFF_ANNOUNCEMENT_DATE: "2025-04-02",
  
  // Chart titles
  OVERALL_PERFORMANCE_TITLE: "Market Performance Since Trump's Inauguration (Jan 20, 2025)",
  TARIFF_IMPACT_TITLE: "Daily Market Changes Following Tariff Announcement (Apr 2, 2025)",
  
  // Number of days to show in the second chart
  DAYS_IN_TARIFF_CHART: 5,
  
  // Specific events to highlight in charts
  EVENTS: [
    { date: "2025-01-20", description: "Inauguration" },
    { date: "2025-01-31", description: "End of January" },
    { date: "2025-02-19", description: "Market Peak" },
    { date: "2025-02-28", description: "End of February" },
    { date: "2025-03-11", description: "Early March Selloff" },
    { date: "2025-03-31", description: "End of March" },
    { date: "2025-04-02", description: "Tariff Announcement" },
    { date: "2025-04-04", description: "China Retaliation" }
  ]
};

// Cache duration in milliseconds (1 day)
export const CACHE_DURATION = 24 * 60 * 60 * 1000;

// Note: Free API keys are limited to 25 requests per day (5 per minute)
// If you need more, consider getting a premium key from https://www.alphavantage.co/premium/ 

// Simulation mode - for development or when API limits are reached
export const SIMULATION_MODE = {
  ENABLED: true,  // Set to true to use simulated data instead of real API data
  START_DATE: "2025-01-20",
  PRICE_VOLATILITY: 1.5  // Controls how volatile the simulated prices are
}; 