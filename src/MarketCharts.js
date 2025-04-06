import React from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, Legend, ResponsiveContainer, BarChart, Bar, ReferenceLine } from 'recharts';

// Card components
import { Card, CardContent, CardHeader, CardTitle } from './components/ui/card';

// Data for Trump's second term inauguration to early April 2025
const marketPerformanceData = [
  { date: 'Jan 20, 2025', sp500: 0, dow: 0, nasdaq: 0, description: 'Inauguration' },
  { date: 'Jan 31, 2025', sp500: -2.1, dow: -1.8, nasdaq: -3.5, description: 'End of January' },
  { date: 'Feb 19, 2025', sp500: 2.6, dow: 1.9, nasdaq: 3.1, description: 'Market Peak' },
  { date: 'Feb 28, 2025', sp500: 0.2, dow: -0.4, nasdaq: -1.1, description: 'End of February' },
  { date: 'Mar 11, 2025', sp500: -3.6, dow: -3.6, nasdaq: -6.8, description: 'Early March Selloff' },
  { date: 'Mar 31, 2025', sp500: -4.6, dow: -5.2, nasdaq: -8.9, description: 'End of March' },
  { date: 'Apr 2, 2025', sp500: -6.1, dow: -6.8, nasdaq: -10.5, description: 'Tariff Announcement' },
  { date: 'Apr 4, 2025', sp500: -10.0, dow: -10.5, nasdaq: -20.3, description: 'Latest Data' },
];

// Data for tariff impact (days surrounding announcement)
const tariffImpactData = [
  { date: 'Apr 1, 2025', sp500Change: 0.8, dowChange: 0.6, nasdaqChange: 1.2, description: 'Pre-Announcement' },
  { date: 'Apr 2, 2025', sp500Change: -0.2, dowChange: -0.3, nasdaqChange: -0.5, description: 'Announcement Day' },
  { date: 'Apr 3, 2025', sp500Change: -4.8, dowChange: -4.0, nasdaqChange: -6.0, description: 'First Trading Day' },
  { date: 'Apr 4, 2025', sp500Change: -6.0, dowChange: -5.5, nasdaqChange: -5.8, description: 'China Retaliation' },
];

function MarketCharts() {
  return (
    <div className="App" style={{ padding: '20px', maxWidth: '1200px', margin: '0 auto' }}>
      <h1>Market Performance Since Trump's Inauguration (2025)</h1>
      
      <div style={{ marginBottom: '40px' }}>
        <h2>Market Performance Since Trump's Inauguration (Jan 20 - Apr 4, 2025)</h2>
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
                formatter={(value, name) => [`${value}%`, name === "sp500" ? "S&P 500" : name === "dow" ? "Dow Jones" : "NASDAQ"]}
                labelFormatter={(label) => {
                  const item = marketPerformanceData.find(d => d.date === label);
                  return `${label} (${item.description})`;
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
        <h2>Daily Market Changes Following Tariff Announcement (Apr 1-4, 2025)</h2>
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
                formatter={(value, name) => [`${value}%`, name === "sp500Change" ? "S&P 500" : name === "dowChange" ? "Dow Jones" : "NASDAQ"]}
                labelFormatter={(label) => {
                  const item = tariffImpactData.find(d => d.date === label);
                  return `${label} (${item.description})`;
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
    </div>
  );
}

export default MarketCharts;