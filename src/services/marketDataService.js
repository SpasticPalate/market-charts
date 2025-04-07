import { ALPHA_VANTAGE_API_KEY, MARKET_SYMBOLS, CACHE_DURATION, TIMEFRAMES, SIMULATION_MODE } from '../config';

// Cache object to store market data
const dataCache = {
  lastUpdated: null,
  marketPerformanceData: null,
  tariffImpactData: null
};

/**
 * Generate simulated market data for testing
 * @param {number} days - Number of days to generate
 * @param {string} startDateStr - Starting date string (YYYY-MM-DD)
 * @returns {Array} - Array of simulated daily time series data
 */
const generateSimulatedData = (days, startDateStr) => {
  const result = [];
  const startDate = new Date(startDateStr);
  
  // Initial price - roughly in the range of the real ETF
  let currentPrice = 450; // Approx. starting price for SPY
  
  for (let i = 0; i < days; i++) {
    const currentDate = new Date(startDate);
    currentDate.setDate(startDate.getDate() + i);
    
    // Skip weekends (no trading on Sat/Sun)
    const dayOfWeek = currentDate.getDay();
    if (dayOfWeek === 0 || dayOfWeek === 6) {
      continue;
    }
    
    // Random price movement (-1.5% to +1.5% daily change)
    const percentChange = (Math.random() - 0.5) * SIMULATION_MODE.PRICE_VOLATILITY;
    currentPrice = currentPrice * (1 + percentChange / 100);
    
    // Certain dates have specific movements (for key events)
    const dateStr = currentDate.toISOString().split('T')[0];
    
    // Simulate specific events
    TIMEFRAMES.EVENTS.forEach(event => {
      if (dateStr === event.date) {
        // Special events can have more dramatic price movements
        if (event.description.includes("Selloff") || event.description.includes("Retaliation")) {
          currentPrice = currentPrice * 0.97; // 3% drop
        } else if (event.description.includes("Peak")) {
          currentPrice = currentPrice * 1.02; // 2% gain
        } else if (event.description.includes("Tariff Announcement")) {
          currentPrice = currentPrice * 0.985; // 1.5% drop
        }
      }
    });
    
    result.push({
      date: dateStr,
      close: parseFloat(currentPrice.toFixed(2)),
      high: parseFloat((currentPrice * 1.01).toFixed(2)),
      low: parseFloat((currentPrice * 0.99).toFixed(2)),
      volume: Math.floor(Math.random() * 10000000) + 40000000
    });
  }
  
  return result;
};

/**
 * Fetch data for a specific market symbol from Alpha Vantage or simulation
 * @param {string} symbol - Market symbol (e.g., SPY for S&P 500 ETF)
 * @returns {Promise<Array>} - Array of daily time series data
 */
const fetchMarketData = async (symbol) => {
  // If simulation mode is enabled, return simulated data
  if (SIMULATION_MODE.ENABLED) {
    console.log(`Using simulated data for ${symbol}`);
    
    // Generate enough simulated days to cover from inauguration to now plus some buffer
    const inaugDate = new Date(TIMEFRAMES.INAUGURATION_DATE);
    const now = new Date();
    const daysDiff = Math.ceil((now - inaugDate) / (1000 * 60 * 60 * 24)) + 30; // Add 30 days buffer
    
    return generateSimulatedData(daysDiff, SIMULATION_MODE.START_DATE);
  }

  try {
    const url = `https://www.alphavantage.co/query?function=TIME_SERIES_DAILY&symbol=${symbol}&apikey=${ALPHA_VANTAGE_API_KEY}&outputsize=compact`;
    console.log(`Fetching data for ${symbol} from:`, url);
    
    const response = await fetch(url);
    
    if (!response.ok) {
      console.error(`HTTP error for ${symbol}:`, response.status, response.statusText);
      throw new Error(`HTTP error! Status: ${response.status}`);
    }
    
    const data = await response.json();
    
    // Check for API error messages
    if (data['Error Message']) {
      console.error(`Alpha Vantage error for ${symbol}:`, data['Error Message']);
      throw new Error(data['Error Message']);
    }
    
    if (data['Information']) {
      console.warn(`Alpha Vantage info for ${symbol}:`, data['Information']);
      // If this is a rate limit message, we might still have data
      if (!data['Time Series (Daily)']) {
        throw new Error(data['Information']);
      }
    }
    
    // Extract the time series data
    const timeSeries = data['Time Series (Daily)'];
    if (!timeSeries) {
      console.error(`No time series data for ${symbol}:`, data);
      throw new Error('No time series data available');
    }
    
    // Convert to array and sort by date
    const timeSeriesArray = Object.entries(timeSeries)
      .map(([date, values]) => ({
        date,
        close: parseFloat(values['4. close']),
        high: parseFloat(values['2. high']),
        low: parseFloat(values['3. low']),
        volume: parseFloat(values['5. volume'])
      }))
      .sort((a, b) => new Date(a.date) - new Date(b.date));
    
    console.log(`Successfully processed ${timeSeriesArray.length} data points for ${symbol}`);
    return timeSeriesArray;
    
  } catch (error) {
    console.error(`Error fetching data for ${symbol}:`, error);
    throw error;
  }
};

/**
 * Map the real current date to a simulated date in 2025
 * This allows us to use actual recent market data but present it as future 2025 data
 * @param {string} realDate - Real date string from API
 * @returns {string} - Mapped date string in 2025
 */
const mapToSimulatedDate = (realDate) => {
  const realDateObj = new Date(realDate);
  
  // Extract month and day, but set the year to 2025
  const month = realDateObj.getMonth();
  const day = realDateObj.getDate();
  
  const simulatedDate = new Date(2025, month, day);
  return simulatedDate.toISOString().split('T')[0];
};

/**
 * Calculate percentage change from a reference date
 * @param {Array} data - Array of price data objects
 * @param {string} referenceDate - Reference date to calculate change from
 * @returns {Array} - Array with percentage changes
 */
const calculatePercentageChanges = (data, referenceDate) => {
  if (!data || data.length === 0) {
    return [];
  }
  
  // Find the reference date index
  const referenceIndex = data.findIndex(item => item.date === referenceDate);
  
  // If reference date not found, use the first available date
  const actualReferenceIndex = referenceIndex !== -1 ? referenceIndex : 0;
  const referencePrice = data[actualReferenceIndex].close;
  
  return data.map(item => ({
    date: item.date,
    percentChange: ((item.close - referencePrice) / referencePrice) * 100
  }));
};

/**
 * Format date for display (e.g., "2025-01-20" to "Jan 20, 2025")
 * @param {string} isoDate - ISO format date string
 * @returns {string} - Formatted date string
 */
const formatDateForDisplay = (isoDate) => {
  const date = new Date(isoDate);
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric'
  });
};

/**
 * Add event descriptions to market data
 * @param {Array} data - Array of market data with dates and percent changes
 * @returns {Array} - Data with descriptions added
 */
const addDescriptionsToData = (data) => {
  // Create a map of specific events from config
  const eventDescriptions = {};
  TIMEFRAMES.EVENTS.forEach(event => {
    eventDescriptions[event.date] = event.description;
  });
  
  // Add default descriptions for dates without specific events
  return data.map(item => {
    const date = item.date;
    let description = eventDescriptions[date];
    
    if (!description) {
      // Check if it's the first trading day of the month
      const itemDate = new Date(date);
      const dayOfMonth = itemDate.getDate();
      const monthName = itemDate.toLocaleDateString('en-US', { month: 'long' });
      
      if (dayOfMonth <= 5) {
        description = `Early ${monthName}`;
      } else if (dayOfMonth >= 25) {
        description = `Late ${monthName}`;
      } else {
        // Check if it's the beginning of a week
        const dayOfWeek = itemDate.getDay();
        if (dayOfWeek === 1) { // Monday
          description = `${monthName} Week ${Math.ceil(dayOfMonth / 7)}`;
        } else {
          description = `${monthName} ${dayOfMonth}`;
        }
      }
    }
    
    return {
      ...item,
      date: formatDateForDisplay(date),
      description
    };
  });
};

/**
 * Get full market performance data with all indices
 * @returns {Promise<Array>} - Array of market performance data
 */
export const getMarketPerformanceData = async () => {
  // Check if cached data is still valid
  const now = new Date().getTime();
  if (
    dataCache.marketPerformanceData && 
    dataCache.lastUpdated && 
    (now - dataCache.lastUpdated) < CACHE_DURATION
  ) {
    console.log('Using cached market performance data');
    return dataCache.marketPerformanceData;
  }
  
  try {
    // Fetch data for each market index
    const [sp500Data, dowData, nasdaqData] = await Promise.all([
      fetchMarketData(MARKET_SYMBOLS.SP500),
      fetchMarketData(MARKET_SYMBOLS.DOW),
      fetchMarketData(MARKET_SYMBOLS.NASDAQ)
    ]);
    
    // Convert inauguration date to a Date object
    const inaugurationDate = new Date(TIMEFRAMES.INAUGURATION_DATE);
    
    // Filter data to only include dates on or after inauguration
    const filteredSP500 = SIMULATION_MODE.ENABLED
      ? sp500Data // If in simulation mode, all data should already be from inauguration date forward
      : sp500Data.filter(item => new Date(item.date) >= inaugurationDate);
    
    const filteredDOW = SIMULATION_MODE.ENABLED
      ? dowData
      : dowData.filter(item => new Date(item.date) >= inaugurationDate);
    
    const filteredNASDAQ = SIMULATION_MODE.ENABLED
      ? nasdaqData
      : nasdaqData.filter(item => new Date(item.date) >= inaugurationDate);
    
    console.log(`Filtered data points - SP500: ${filteredSP500.length}, DOW: ${filteredDOW.length}, NASDAQ: ${filteredNASDAQ.length}`);
    
    // Make sure we have some data
    if (filteredSP500.length === 0 || filteredDOW.length === 0 || filteredNASDAQ.length === 0) {
      throw new Error('Insufficient data for one or more indices');
    }
    
    // Calculate percentage changes from inauguration date
    const sp500Changes = calculatePercentageChanges(filteredSP500, TIMEFRAMES.INAUGURATION_DATE);
    const dowChanges = calculatePercentageChanges(filteredDOW, TIMEFRAMES.INAUGURATION_DATE);
    const nasdaqChanges = calculatePercentageChanges(filteredNASDAQ, TIMEFRAMES.INAUGURATION_DATE);
    
    // Combine data from all indices into a single array
    // Only include dates where we have data for all three indices
    const combinedData = [];
    
    for (let i = 0; i < sp500Changes.length; i++) {
      const date = sp500Changes[i].date;
      const dowItem = dowChanges.find(item => item.date === date);
      const nasdaqItem = nasdaqChanges.find(item => item.date === date);
      
      if (dowItem && nasdaqItem) {
        combinedData.push({
          date,
          sp500: parseFloat(sp500Changes[i].percentChange.toFixed(2)),
          dow: parseFloat(dowItem.percentChange.toFixed(2)),
          nasdaq: parseFloat(nasdaqItem.percentChange.toFixed(2))
        });
      }
    }
    
    // Map real dates to simulated 2025 dates if we're not in simulation mode
    // This is only needed when using real API data but wanting to display it as 2025 data
    let mappedData = combinedData;
    if (!SIMULATION_MODE.ENABLED) {
      // This mapping step is optional, can be commented out if you want to show real calendar dates
      // mappedData = combinedData.map(item => ({
      //   ...item,
      //   date: mapToSimulatedDate(item.date)
      // }));
    }
    
    // Add descriptions and format dates
    const formattedData = addDescriptionsToData(mappedData);
    
    // Update cache
    dataCache.marketPerformanceData = formattedData;
    dataCache.lastUpdated = now;
    
    return formattedData;
    
  } catch (error) {
    console.error('Error getting market performance data:', error);
    // Return empty array or last cached data if available
    return dataCache.marketPerformanceData || [];
  }
};

/**
 * Get market data specifically for the tariff impact view
 * @returns {Promise<Array>} - Array of tariff impact data
 */
export const getTariffImpactData = async () => {
  // Check if cached data is still valid
  const now = new Date().getTime();
  if (
    dataCache.tariffImpactData && 
    dataCache.lastUpdated && 
    (now - dataCache.lastUpdated) < CACHE_DURATION
  ) {
    console.log('Using cached tariff impact data');
    return dataCache.tariffImpactData;
  }
  
  try {
    // Get the full market data first
    const allData = await getMarketPerformanceData();
    
    if (allData.length === 0) {
      throw new Error('No market data available');
    }
    
    // Find the index of the tariff announcement date
    const tariffDateFormatted = formatDateForDisplay(TIMEFRAMES.TARIFF_ANNOUNCEMENT_DATE);
    const tariffIndex = allData.findIndex(item => 
      item.description.includes("Tariff Announcement") || 
      item.date === tariffDateFormatted
    );
    
    // If tariff date not found, get the last few days
    let tariffData;
    if (tariffIndex === -1) {
      // Take the last N days
      tariffData = allData.slice(-TIMEFRAMES.DAYS_IN_TARIFF_CHART);
    } else {
      // Take data from tariff date forward
      const daysToInclude = Math.min(TIMEFRAMES.DAYS_IN_TARIFF_CHART, allData.length - tariffIndex);
      tariffData = allData.slice(tariffIndex, tariffIndex + daysToInclude);
    }
    
    console.log(`Processing ${tariffData.length} days of tariff impact data`);
    
    // Transform data for the tariff impact view (daily changes instead of cumulative)
    const transformedData = [];
    
    for (let i = 0; i < tariffData.length; i++) {
      const current = tariffData[i];
      const previous = i > 0 ? tariffData[i - 1] : null;
      
      const dailyChange = {
        date: current.date,
        description: current.description,
        sp500Change: previous 
          ? parseFloat((current.sp500 - previous.sp500).toFixed(2)) 
          : 0,
        dowChange: previous 
          ? parseFloat((current.dow - previous.dow).toFixed(2)) 
          : 0,
        nasdaqChange: previous 
          ? parseFloat((current.nasdaq - previous.nasdaq).toFixed(2)) 
          : 0
      };
      
      transformedData.push(dailyChange);
    }
    
    // Update cache
    dataCache.tariffImpactData = transformedData;
    
    return transformedData;
    
  } catch (error) {
    console.error('Error getting tariff impact data:', error);
    // Return empty array or last cached data if available
    return dataCache.tariffImpactData || [];
  }
}; 