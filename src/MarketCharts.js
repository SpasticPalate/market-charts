import React, { useState, useEffect } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, BarChart, Bar, ReferenceLine } from 'recharts';

// Import our market data service
import { getMarketPerformanceData, getTariffImpactData } from './services/marketDataService';
import { ALPHA_VANTAGE_API_KEY, TIMEFRAMES, SIMULATION_MODE } from './config';

function MarketCharts() {
  // State for market data
  const [marketPerformanceData, setMarketPerformanceData] = useState([]);
  const [tariffImpactData, setTariffImpactData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  // Load data from API
  useEffect(() => {
    const loadData = async () => {
      try {
        setLoading(true);
        
        // Check if API key has been set
        if (ALPHA_VANTAGE_API_KEY === "YOUR_API_KEY_HERE" && !SIMULATION_MODE.ENABLED) {
          throw new Error("Please set your Alpha Vantage API key in src/config.js or enable simulation mode");
        }
        
        // Get market performance data since inauguration
        const performanceData = await getMarketPerformanceData();
        
        if (performanceData.length === 0) {
          throw new Error("No market performance data received from API");
        }
        
        setMarketPerformanceData(performanceData);
        
        // Get tariff impact data
        const tariffData = await getTariffImpactData();
        
        if (tariffData.length === 0) {
          throw new Error("No tariff impact data received from API");
        }
        
        setTariffImpactData(tariffData);
        setError(null);
      } catch (err) {
        console.error('Error loading market data:', err);
        setError(`Failed to load market data: ${err.message}`);
        
        // Use fallback data if API fails
        setMarketPerformanceData([
          { date: 'Jan 20, 2025', sp500: 0, dow: 0, nasdaq: 0, description: 'Inauguration' },
          { date: 'Jan 31, 2025', sp500: -2.1, dow: -1.8, nasdaq: -3.5, description: 'End of January' },
          { date: 'Feb 19, 2025', sp500: 2.6, dow: 1.9, nasdaq: 3.1, description: 'Market Peak' },
          { date: 'Feb 28, 2025', sp500: 0.2, dow: -0.4, nasdaq: -1.1, description: 'End of February' },
          { date: 'Apr 4, 2025', sp500: -10.0, dow: -10.5, nasdaq: -20.3, description: 'Latest Data' },
        ]);
        
        setTariffImpactData([
          { date: 'Apr 1, 2025', sp500Change: 0.8, dowChange: 0.6, nasdaqChange: 1.2, description: 'Pre-Announcement' },
          { date: 'Apr 2, 2025', sp500Change: -0.2, dowChange: -0.3, nasdaqChange: -0.5, description: 'Tariff Announcement' },
          { date: 'Apr 3, 2025', sp500Change: -4.8, dowChange: -4.0, nasdaqChange: -6.0, description: 'First Trading Day' },
          { date: 'Apr 4, 2025', sp500Change: -6.0, dowChange: -5.5, nasdaqChange: -5.8, description: 'China Retaliation' },
        ]);
      } finally {
        setLoading(false);
      }
    };
    
    loadData();
  }, []);

  if (loading) {
    return (
      <div className="App" style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto', textAlign: 'center' }}>
        <h1>Loading Market Data...</h1>
        <p>Fetching the latest market data from Alpha Vantage...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="App" style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto', textAlign: 'center' }}>
        <h1>Error Loading Data</h1>
        <p style={{ color: 'red' }}>{error}</p>
        <p>Displaying fallback data instead.</p>
        <p style={{ fontSize: '0.9rem', marginTop: '20px' }}>
          Troubleshooting tips:
          <ul style={{ textAlign: 'left', listStylePosition: 'inside' }}>
            <li>Make sure you've set your Alpha Vantage API key in src/config.js</li>
            <li>Check the browser console for detailed error messages</li>
            <li>The free API tier is limited to 25 requests per day - you may have reached your limit</li>
            <li>Consider enabling simulation mode in config.js if you're experiencing API rate limiting</li>
          </ul>
        </p>
      </div>
    );
  }

  return (
    <div className="App" style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
      <h1>Market Performance Dashboard</h1>
      
      <div style={{ marginBottom: '40px' }}>
        <h2>{TIMEFRAMES.OVERALL_PERFORMANCE_TITLE}</h2>
        <div style={{ height: '400px' }}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart
              data={marketPerformanceData}
              margin={{ top: 5, right: 30, left: 20, bottom: 5 }}
            >
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis
                label={{ value: 'Percent Change (%)', angle: -90, position: 'insideLeft' }}
                domain={['dataMin - 2', 'dataMax + 2']}
              />
              <Tooltip 
                formatter={(value, name) => {
                  // Fix for tooltip issue - map each dataKey to the correct name
                  const mappedName = 
                    name === "sp500" ? "S&P 500" : 
                    name === "dow" ? "Dow Jones" : 
                    name === "nasdaq" ? "NASDAQ" : name;
                  return [`${value}%`, mappedName];
                }}
                labelFormatter={(label) => {
                  const item = marketPerformanceData.find(d => d.date === label);
                  return item ? `${label} (${item.description})` : label;
                }}
              />
              <Legend />
              <ReferenceLine y={0} stroke="#000" />
              <Line type="monotone" dataKey="sp500" name="S&P 500" stroke="#8884d8" activeDot={{ r: 8 }} />
              <Line type="monotone" dataKey="dow" name="Dow Jones" stroke="#82ca9d" />
              <Line type="monotone" dataKey="nasdaq" name="NASDAQ" stroke="#ff7300" />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div style={{ marginBottom: '40px' }}>
        <h2>{TIMEFRAMES.TARIFF_IMPACT_TITLE}</h2>
        <div style={{ height: '400px' }}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart
              data={tariffImpactData}
              margin={{ top: 5, right: 30, left: 20, bottom: 5 }}
            >
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" />
              <YAxis
                label={{ value: 'Daily Percent Change (%)', angle: -90, position: 'insideLeft' }}
                domain={['dataMin - 1', 'dataMax + 1']}
              />
              <Tooltip 
                formatter={(value, name) => {
                  // Fix for tooltip issue - map each dataKey to the correct name
                  const mappedName = 
                    name === "sp500Change" ? "S&P 500" : 
                    name === "dowChange" ? "Dow Jones" : 
                    name === "nasdaqChange" ? "NASDAQ" : name;
                  return [`${value}%`, mappedName];
                }}
                labelFormatter={(label) => {
                  const item = tariffImpactData.find(d => d.date === label);
                  return item ? `${label} (${item.description})` : label;
                }}
              />
              <Legend />
              <ReferenceLine y={0} stroke="#000" />
              <Bar dataKey="sp500Change" name="S&P 500" fill="#8884d8" />
              <Bar dataKey="dowChange" name="Dow Jones" fill="#82ca9d" />
              <Bar dataKey="nasdaqChange" name="NASDAQ" fill="#ff7300" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </div>
      
      <div style={{ fontSize: '0.8rem', color: '#666', marginTop: '40px', textAlign: 'center' }}>
        <p>
          Data source: {SIMULATION_MODE.ENABLED ? 'Simulation Mode' : 'Alpha Vantage API'} | 
          Last updated: {new Date().toLocaleDateString()} {new Date().toLocaleTimeString()}
        </p>
      </div>
    </div>
  );
}

export default MarketCharts;