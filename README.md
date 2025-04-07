# Market Charts Application

A React application that displays market performance data for major stock indices (S&P 500, Dow Jones, and NASDAQ) focusing on two key periods:

1. Performance since Trump's second inauguration (Jan 20, 2025)
2. Impact of tariff announcements on the markets (Apr 2, 2025)

## Features

- Real-time market data visualization using Alpha Vantage API
- Percentage change tracking from Trump's inauguration date
- Daily market changes following tariff announcements
- Configurable event dates and descriptions
- Simulation mode for development or when API limits are reached
- Intelligent caching to minimize API calls

## Setup Instructions

1. Clone this repository
2. Install dependencies with `npm install`
3. Configure your Alpha Vantage API key:
   - Get a free API key from [Alpha Vantage](https://www.alphavantage.co/support/#api-key)
   - Open `src/config.js` and replace `YOUR_API_KEY_HERE` with your actual API key

   ```javascript
   export const ALPHA_VANTAGE_API_KEY = "YOUR_API_KEY_HERE"; // Replace with your actual API key
   ```

4. Start the application with `npm start`
5. The application will automatically fetch and display the latest market data

## Configuration Options

You can modify various settings in `src/config.js`:

### API Configuration
- `ALPHA_VANTAGE_API_KEY`: Your API key for Alpha Vantage
- `MARKET_SYMBOLS`: Stock symbols for the indices to track

### Time Frame Settings
- `INAUGURATION_DATE`: Trump's inauguration date (reference point for first chart)
- `TARIFF_ANNOUNCEMENT_DATE`: Date when tariffs were announced (reference for second chart)
- `OVERALL_PERFORMANCE_TITLE`: Title for the first chart
- `TARIFF_IMPACT_TITLE`: Title for the second chart
- `DAYS_IN_TARIFF_CHART`: Number of days to show in the tariff impact chart
- `EVENTS`: Array of specific events to highlight in the charts

### Simulation Mode
If you want to develop without using API calls or have reached API limits:

```javascript
export const SIMULATION_MODE = {
  ENABLED: true,  // Set to true to use simulated data instead of API data
  START_DATE: "2025-01-20",
  PRICE_VOLATILITY: 1.5
};
```

## Data Handling

- Data is fetched from Alpha Vantage API when the application loads
- A caching mechanism prevents excessive API calls (data is cached for 24 hours by default)
- If the API request fails, fallback data is displayed
- Simulation mode can be enabled to generate realistic market data without API calls

## Getting Started with Create React App

This project was bootstrapped with [Create React App](https://github.com/facebook/create-react-app).

## Available Scripts

In the project directory, you can run:

### `npm start`

Runs the app in the development mode.\
Open [http://localhost:3000](http://localhost:3000) to view it in your browser.

The page will reload when you make changes.\
You may also see any lint errors in the console.

### `npm test`

Launches the test runner in the interactive watch mode.\
See the section about [running tests](https://facebook.github.io/create-react-app/docs/running-tests) for more information.

### `npm run build`

Builds the app for production to the `build` folder.\
It correctly bundles React in production mode and optimizes the build for the best performance.

The build is minified and the filenames include the hashes.\
Your app is ready to be deployed!

See the section about [deployment](https://facebook.github.io/create-react-app/docs/deployment) for more information.

### `npm run eject`

**Note: this is a one-way operation. Once you `eject`, you can't go back!**

If you aren't satisfied with the build tool and configuration choices, you can `eject` at any time. This command will remove the single build dependency from your project.

Instead, it will copy all the configuration files and the transitive dependencies (webpack, Babel, ESLint, etc) right into your project so you have full control over them. All of the commands except `eject` will still work, but they will point to the copied scripts so you can tweak them. At this point you're on your own.

You don't have to ever use `eject`. The curated feature set is suitable for small and middle deployments, and you shouldn't feel obligated to use this feature. However we understand that this tool wouldn't be useful if you couldn't customize it when you are ready for it.

## Learn More

You can learn more in the [Create React App documentation](https://facebook.github.io/create-react-app/docs/getting-started).

To learn React, check out the [React documentation](https://reactjs.org/).

### Code Splitting

This section has moved here: [https://facebook.github.io/create-react-app/docs/code-splitting](https://facebook.github.io/create-react-app/docs/code-splitting)

### Analyzing the Bundle Size

This section has moved here: [https://facebook.github.io/create-react-app/docs/analyzing-the-bundle-size](https://facebook.github.io/create-react-app/docs/analyzing-the-bundle-size)

### Making a Progressive Web App

This section has moved here: [https://facebook.github.io/create-react-app/docs/making-a-progressive-web-app](https://facebook.github.io/create-react-app/docs/making-a-progressive-web-app)

### Advanced Configuration

This section has moved here: [https://facebook.github.io/create-react-app/docs/advanced-configuration](https://facebook.github.io/create-react-app/docs/advanced-configuration)

### Deployment

This section has moved here: [https://facebook.github.io/create-react-app/docs/deployment](https://facebook.github.io/create-react-app/docs/deployment)

### `npm run build` fails to minify

This section has moved here: [https://facebook.github.io/create-react-app/docs/troubleshooting#npm-run-build-fails-to-minify](https://facebook.github.io/create-react-app/docs/troubleshooting#npm-run-build-fails-to-minify)
