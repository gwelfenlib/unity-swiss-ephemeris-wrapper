# Unity Swiss Ephemeris Wrapper

Unity C# wrapper for the Swiss Ephemeris library providing accurate astrological calculations.

## Features

- Planet position calculations for all major celestial bodies
- Multiple house systems: Koch, Placidus, Equal
- Tropical and Sidereal zodiac support
- Additional astrological points: Nodes, Chiron, Lilith, Proserpine
- Clean API with comprehensive error handling
- High precision calculations based on Swiss Ephemeris

## Supported Celestial Bodies

**Primary Planets**
Sun, Moon, Mercury, Venus, Mars, Jupiter, Saturn, Uranus, Neptune, Pluto

**Additional Points**
True North/South Nodes, Chiron, Mean Black Moon Lilith, Proserpina

## House Systems

- Koch Houses (default)
- Placidus Houses  
- Equal Houses (30° divisions from Ascendant)

## Installation

**Git Submodule (Recommended)**
```bash
git submodule add https://github.com/gwelfenlib/unity-swiss-ephemeris-wrapper.git Assets/SwissEphemerisWrapper
git submodule update --init --recursive
```

**Manual Installation**
1. Download repository contents
2. Place in Unity project Assets/ folder
3. Ensure SwissEphNet.dll reference

## Usage

```csharp
using UnityEngine;
using System;

public class AstrologyExample : MonoBehaviour
{
    public PlanetCalculator calculator;
    
    void Start()
    {
        // Create birth data
        var birthData = new BirthData
        {
            birthDate = new DateTime(1990, 6, 15, 14, 30, 0),
            latitude = 55.7558f, 
            longitude = 37.6176f,
            timezone = 3f       
        };
        
        // Subscribe to calculation completion
        calculator.OnChartCalculated += OnChartReady;
        
        // Calculate natal chart
        calculator.CalculateNatalChart(birthData);
    }
    
    void OnChartReady(NatalChart chart)
    {
        Debug.Log($"Sun position: {chart.planets[0].longitude:F2}°");
        Debug.Log($"Ascendant: {chart.ascendant.longitude:F2}°");
    }
}
```

## Data Structures

**BirthData**
```csharp
public class BirthData
{
    public DateTime birthDate;  // Birth date and time
    public float latitude;      // Geographic latitude (-90 to +90)
    public float longitude;     // Geographic longitude (-180 to +180)
    public float timezone;      // UTC offset (-12 to +14)
}
```

**NatalChart**
```csharp
public class NatalChart
{
    public BirthData birthData;
    public List<PlanetPosition> planets;
    public List<HousePosition> houses;
    public AnglePosition ascendant;
    public AnglePosition midheaven;
    public double julianDay;
    public float ayanamsa;
}
```

## Coordinate Systems

**Tropical Zodiac** - Aligned with Earth's seasons (0° Aries = Spring Equinox)
**Sidereal Zodiac** - Aligned with fixed stars, uses Lahiri Ayanamsa

## Date Range

Supports calculations from approximately 13,000 BCE to 17,000 CE.
Accuracy decreases for extreme dates outside ephemeris range.

## License

Licensed under GPL-3.0 due to Swiss Ephemeris licensing requirements.

**Swiss Ephemeris**
Copyright © 1997-2008 Astrodienst AG, Switzerland
Authors: Dieter Koch and Alois Treindl
License: GNU General Public License v2

**Wrapper**
Copyright © 2025 GwelfenLib
Licensed under GPL-3.0

## Contributing

1. Fork the repository
2. Create feature branch
3. Add tests for new functionality  
4. Submit pull request

---

This wrapper provides a Unity-friendly interface to Swiss Ephemeris calculations.
