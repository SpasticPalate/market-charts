# Market Charts Web Application Requirements Document

## Project Overview

A simple web application that tracks and displays the performance of major American stock market indices (S&P 500, Dow Jones, NASDAQ) from specific timeframes in 2025. The application, called "Market Charts", will automatically retrieve and cache stock market data, presenting it in an easy-to-read visual format.

## User Stories

### Core Functionality

1. **As a user**, I want to view the performance of major US stock indices (S&P 500, Dow Jones, NASDAQ) from January 20, 2025 (Trump's second term inauguration) to the current date, so that I can track market performance during this administration.
2. **As a user**, I want to view the performance of major US stock indices from early April 2025 (pre-tariff announcement) to the current date, so that I can assess market reaction to the tariff policies.
3. **As a user**, I want the application to automatically fetch the latest data when I visit the page, so that I always see up-to-date information without manual intervention.
4. **As a user**, I want the application to use cached data when available, so that page loads are fast and efficient.
5. **As a user**, I want the option to compare current market trends with previous administrations, so that I can contextualize the current performance.

### Technical Functionality

1. **As a developer**, I need to implement a reliable API integration for retrieving stock market data with a backup API option, so that the application remains functional if the primary API fails or reaches its limits.
2. **As a developer**, I need to set up a lightweight database to cache fetched market data, so that we minimize external API calls and improve page load performance.
3. **As a developer**, I need to implement responsive design using Tailwind CSS, so that the application is usable on both desktop and mobile devices.
4. **As a developer**, I need to ensure the application works within a Vercel hosting environment pulled from GitHub, so that it aligns with the existing deployment workflow.

## Acceptance Criteria

### User Story 1 & 2: View Market Performance

- The application displays two separate charts for the specified time periods
- Each chart clearly indicates which indices are being displayed
- Charts include appropriate labels for axes (dates and values)
- Charts include a legend to identify each market index
- Charts should be visually distinct to avoid confusion between the two time periods
- Technical indicators that aid understanding are included on the charts
- Charts load without errors and display correct data

### User Story 3 & 4: Data Fetching and Caching

- When the application loads, it checks if yesterday's market data exists in the database
- If data is missing, the application automatically fetches it from the primary API
- If the primary API fails, the application attempts to use the backup API
- Once data is retrieved from an API, it is properly stored in the database
- Subsequent page loads retrieve data from the database without making unnecessary API calls
- On initial application load, all historical data from January 20, 2025 is retrieved and stored

### User Story 5: Market Comparison

- The application includes a checkbox option to enable comparison with previous administrations
- When enabled, the first chart (inauguration to present) shows comparison data
- Comparison data is clearly differentiated from current data
- The comparison feature does not interfere with the primary chart visualization

### Technical Requirements

- The application successfully integrates with at least one free stock market data API
- A backup API is configured and automatically used when the primary API fails
- A lightweight database (e.g., SQLite, LiteDB) is implemented for data caching
- The application is built using C#
- The UI is implemented using Tailwind CSS
- The application is responsive and functions properly on mobile devices
- The application can be successfully deployed to Vercel from GitHub

## Technical Specifications

### Front-End

- **Technology**: C# with Blazor WebAssembly
- **CSS Framework**: Tailwind CSS
- **Chart Library**: Recommendation to use Blazor-compatible charting libraries like Radzen.Blazor or ChartJs.Blazor

### Back-End

- **Language**: C#
- **API Integration**: RESTful API calls to stock market data providers
- **Recommended APIs**:
  - Primary: Alpha Vantage (free tier offers daily data)
  - Backup: Yahoo Finance API or Finnhub (also offer free tiers)
- **Database**: SQLite or LiteDB for lightweight local storage
- **Hosting**: Vercel via GitHub deployment

### Data Structure

- **Stock Data Model**:

  ```csharp
  public class StockIndexData
  {
      public int Id { get; set; }
      public DateTime Date { get; set; }
      public string IndexName { get; set; } // "S&P 500", "Dow Jones", "NASDAQ"
      public decimal CloseValue { get; set; }
      public decimal OpenValue { get; set; }
      public decimal HighValue { get; set; }
      public decimal LowValue { get; set; }
      public long Volume { get; set; }
      public DateTime FetchedAt { get; set; }
  }
  ```

- **API Response Handler**:

  ```csharp
  public interface IStockApiService
  {
      Task<List<StockIndexData>> GetHistoricalDataAsync(string indexSymbol, DateTime startDate, DateTime endDate);
      Task<StockIndexData> GetLatestDataAsync(string indexSymbol);
  }
  
  public class PrimaryApiService : IStockApiService { /* Implementation */ }
  public class BackupApiService : IStockApiService { /* Implementation */ }
  ```

### API Information

- **Alpha Vantage API**:
  - Base URL: `https://www.alphavantage.co/query`
  - Parameters for daily data: `function=TIME_SERIES_DAILY&symbol=<index_symbol>&apikey=<your_api_key>`
  - Index symbols: `^GSPC` (S&P 500), `^DJI` (Dow Jones), `^IXIC` (NASDAQ)
  - Free tier allows 25 API calls per day

- **StockData.org API** (backup):
  - Base URL: `https://api.stockdata.org/v1/data/quote`
  - Parameters for stock quotes: `symbols=<index_symbols>&api_token=<your_api_key>`
  - Index symbols can be passed as comma-separated values (e.g., `symbols=^GSPC,^DJI,^IXIC`)
  - Free tier offers 100 requests daily

### Data Flow

1. Check if yesterday's data exists in database
2. If missing, try primary API
3. If primary API fails, try backup API
4. Store retrieved data in database
5. Use stored data for chart visualization

## UI Design Recommendations

### Layout

- Clean, minimalist design with white background and subtle shadows
- Two main chart components taking center stage
- Clear headings indicating the time period for each chart
- Responsive design that works well on mobile (charts stack vertically) and desktop (charts side by side)

### Chart Design

- Line charts with smooth curves for easy reading
- Each index represented by a different color with a clear legend
- Optional shaded area under lines for better visual distinction
- Date range selector to allow zooming into specific periods
- Tooltip on hover showing exact values for each index on a given date

### Color Scheme

- S&P 500: #10B981 (Emerald)
- Dow Jones: #3B82F6 (Blue)
- NASDAQ: #8B5CF6 (Purple)
- UI Accents: #F59E0B (Amber)
- Background: White with subtle gray sections

### Comparison Feature

- Simple checkbox labeled "Compare with previous administration"
- When checked, adds lighter-colored lines to the inauguration chart showing previous administration data
- Clear visual distinction between current and comparison data

## Implementation Notes

1. **API Key Management**: Store API keys in environment variables or a secure configuration
2. **Error Handling**: Implement robust error handling for API failures
3. **Data Validation**: Validate incoming API data before storing in the database
4. **Caching Strategy**: Implement efficient caching to minimize API calls
5. **Performance Optimization**: Optimize chart rendering for large datasets
6. **Mobile Responsiveness**: Ensure charts are readable on smaller screens
7. **Initial Load**: Show loading indicator during initial historical data fetch
8. **Code Organization**: Follow clean architecture principles with separation of concerns

## Deployment Guidelines

1. Use GitHub repository for version control
2. Configure CI/CD pipeline for automated deployment to Vercel
3. Set up environment variables in Vercel for API keys
4. Ensure proper database initialization on first deployment

## Blazor WebAssembly Deployment Notes for Vercel

Since this is a Blazor WebAssembly application being deployed to Vercel:

1. Blazor WebAssembly apps are compiled to static files that can be hosted on static site providers like Vercel
2. Create appropriate build scripts in your repository to ensure Vercel can build the application
3. Ensure proper configuration for routing in Vercel to handle Blazor's routing system
4. Consider adding a `vercel.json` configuration file if needed for custom settings
5. Make sure all API keys and configurations are properly set as environment variables in Vercel
6. Test the deployment process in a staging environment before finalizing
