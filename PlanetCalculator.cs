using UnityEngine;
using System;
using System.Collections.Generic;
using SwissEphNet;

/// <summary>
/// Planetary calculator using Swiss Ephemeris library for astrological computations.
/// Calculates planet positions, house cusps, and chart angles for natal charts.
/// Supports both Tropical and Sidereal zodiac systems with multiple house systems.
/// </summary>
public class PlanetCalculator : MonoBehaviour
{
    #region CONSTANTS

    // Ayanamsa calculation constants
    private const double AYANAMSA_BASE_YEAR = 285.0; // Reference year (285 CE)
    private const double PRECESSION_PER_YEAR = 50.29 / 3600.0; // Degrees per year (50.29 arcseconds)

    // Swiss Ephemeris date range limits
    private const double MIN_JULIAN_DAY = 625673.5;   // Approximate start of ephemeris range (-13000 CE)
    private const double MAX_JULIAN_DAY = 2816787.5;  // Approximate end of ephemeris range (+17191 CE)

    // Planet calculation flags
    private const int CALCULATION_FLAGS = SwissEph.SEFLG_SPEED;
    private const int CALENDAR_TYPE = SwissEph.SE_GREG_CAL;

    #endregion

    #region SERIALIZED FIELDS

    [SerializeField] private AstrologyController general;

    [Header("Calculation Settings")]
    public bool useSiderealZodiac = false;
    [SerializeField] private float currentAyanamsa;

    #endregion

    #region PRIVATE FIELDS

    private SwissEph swissEph;

    #endregion

    #region EVENTS

    public event System.Action<NatalChart> OnChartCalculated;
    public event System.Action<string> OnCalculationError;

    #endregion

    #region UNITY LIFECYCLE

    /// <summary>
    /// Initialize Swiss Ephemeris engine on component creation
    /// </summary>
    private void Awake()
    {
        InitializeSwissEphemeris();
    }

    /// <summary>
    /// Clean up Swiss Ephemeris resources on component destruction
    /// </summary>
    private void OnDestroy()
    {
        CleanupSwissEphemeris();
    }

    #endregion

    #region INITIALIZATION

    /// <summary>
    /// Initialize Swiss Ephemeris calculation engine with error handling
    /// </summary>
    private void InitializeSwissEphemeris()
    {
        try
        {
            swissEph = new SwissEph();
        }
        catch (Exception ex)
        {
            HandleCalculationError($"Swiss Ephemeris initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Safely dispose of Swiss Ephemeris resources
    /// </summary>
    private void CleanupSwissEphemeris()
    {
        try
        {
            swissEph?.Dispose();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error during Swiss Ephemeris cleanup: {ex.Message}");
        }
    }

    #endregion

    #region PUBLIC API

    /// <summary>
    /// Calculate complete natal chart from birth data including planets, houses, and angles
    /// </summary>
    /// <param name="birthData">Birth information containing date, time, and location</param>
    public void CalculateNatalChart(BirthData birthData)
    {
        if (!ValidateCalculationPreconditions(birthData))
            return;

        try
        {
            var calculationSettings = GetCalculationSettings();
            var chart = CreateNatalChart(birthData, calculationSettings);

            OnChartCalculated?.Invoke(chart);
        }
        catch (Exception ex)
        {
            HandleCalculationError($"Chart calculation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get description of current calculation system for display purposes
    /// </summary>
    /// <returns>Formatted string describing zodiac and house system</returns>
    public string GetCalculationSystemInfo()
    {
        var settings = GetCalculationSettings();
        string zodiacType = settings.UseTropical ? "Tropical" : "Sidereal";
        string houseSystemName = GetHouseSystemDisplayName(settings.HouseSystem);

        if (settings.UseTropical)
        {
            return $"{zodiacType}\n{houseSystemName}";
        }
        else
        {
            return $"{zodiacType} {currentAyanamsa:F0}°\n{houseSystemName}";
        }
    }

    /// <summary>
    /// Get formatted planet information string for display
    /// </summary>
    /// <param name="planet">Planet position data</param>
    /// <returns>Formatted planet information</returns>
    public string GetPlanetInfo(PlanetPosition planet)
    {
        string retrogradeIndicator = planet.isRetrograde ? " (R)" : "";
        return $"{planet.planetType}: {planet.longitude:F1}° - {planet.zodiacSign} {planet.degreeInSign:F1}°{retrogradeIndicator}";
    }

    #endregion

    #region CHART CALCULATION

    /// <summary>
    /// Create complete natal chart with all celestial calculations
    /// </summary>
    private NatalChart CreateNatalChart(BirthData birthData, CalculationSettings settings)
    {
        var chart = new NatalChart
        {
            birthData = birthData,
            ayanamsa = currentAyanamsa
        };

        chart.julianDay = CalculateJulianDay(birthData);
        CalculateAllPlanets(chart, chart.julianDay, settings);
        CalculateHouses(chart, chart.julianDay, birthData.latitude, birthData.longitude, settings.HouseSystem);

        UpdateSystemDisplay(settings);

        return chart;
    }

    /// <summary>
    /// Get current calculation settings from database
    /// </summary>
    private CalculationSettings GetCalculationSettings()
    {
        bool useTropical = general.database.GetUseTropicalZodiac();
        HouseSystem houseSystem = general.database.GetHouseSystem();

        useSiderealZodiac = !useTropical;
        currentAyanamsa = CalculateAyanamsa(DateTime.Now.Year);

        return new CalculationSettings
        {
            UseTropical = useTropical,
            HouseSystem = houseSystem,
            Ayanamsa = currentAyanamsa
        };
    }

    /// <summary>
    /// Update system calculation display with current settings
    /// </summary>
    private void UpdateSystemDisplay(CalculationSettings settings)
    {
        if (general?.systemCalculation != null)
        {
            general.systemCalculation.text = GetCalculationSystemInfo();
        }
    }

    #endregion

    #region JULIAN DAY CALCULATION

    /// <summary>
    /// Calculate Julian Day from birth data with UTC conversion and validation
    /// </summary>
    /// <param name="birthData">Birth information</param>
    /// <returns>Julian Day number for astronomical calculations</returns>
    private double CalculateJulianDay(BirthData birthData)
    {
        DateTime utcTime = ConvertToUTC(birthData);
        double fractionalHour = CalculateFractionalHour(utcTime);

        double julianDay = swissEph.swe_julday(
            utcTime.Year,
            utcTime.Month,
            utcTime.Day,
            fractionalHour,
            CALENDAR_TYPE
        );

        ValidateJulianDay(julianDay, birthData.birthDate.Year);

        return julianDay;
    }

    /// <summary>
    /// Convert local birth time to UTC
    /// </summary>
    private DateTime ConvertToUTC(BirthData birthData)
    {
        return birthData.birthDate.AddHours(-birthData.timezone);
    }

    /// <summary>
    /// Calculate fractional hour from time components
    /// </summary>
    private double CalculateFractionalHour(DateTime time)
    {
        return time.Hour + (time.Minute / 60.0) + (time.Second / 3600.0);
    }

    /// <summary>
    /// Validate Julian Day is within ephemeris range
    /// </summary>
    private void ValidateJulianDay(double julianDay, int year)
    {
        if (julianDay < MIN_JULIAN_DAY || julianDay > MAX_JULIAN_DAY)
        {
            Debug.LogWarning($"Date {year} may be outside Swiss Ephemeris range. Calculation may be less accurate.");
        }
    }

    #endregion

    #region AYANAMSA CALCULATION

    /// <summary>
    /// Calculate Lahiri Ayanamsa for given year using standard precession rate
    /// </summary>
    /// <param name="year">Year for calculation</param>
    /// <returns>Ayanamsa value in degrees</returns>
    private float CalculateAyanamsa(int year)
    {
        double yearsDifference = year - AYANAMSA_BASE_YEAR;
        double ayanamsa = yearsDifference * PRECESSION_PER_YEAR;

        // Normalize to 0-360 degree range
        ayanamsa = NormalizeAngle(ayanamsa);

        // Issue warnings for extreme dates
        ValidateAyanamsaYear(year);

        return (float)ayanamsa;
    }

    /// <summary>
    /// Normalize angle to 0-360 degree range
    /// </summary>
    private double NormalizeAngle(double angle)
    {
        while (angle < 0) angle += 360.0;
        while (angle >= 360.0) angle -= 360.0;
        return angle;
    }

    /// <summary>
    /// Validate year for ayanamsa calculation accuracy
    /// </summary>
    private void ValidateAyanamsaYear(int year)
    {
        if (year < -2000)
        {
            Debug.LogWarning($"Ancient year {year}: Ayanamsa precision may vary for extreme dates");
        }
        else if (year > 3000)
        {
            Debug.LogWarning($"Future year {year}: Ayanamsa is extrapolated");
        }
    }

    #endregion

    #region PLANET CALCULATIONS

    /// <summary>
    /// Calculate positions for all planets and additional astrological points
    /// </summary>
    private void CalculateAllPlanets(NatalChart chart, double julianDay, CalculationSettings settings)
    {
        chart.planets = new List<PlanetPosition>();

        // Calculate primary planets
        CalculatePrimaryPlanets(chart, julianDay, settings);

        // Calculate additional astrological points
        CalculateAdditionalPoints(chart, julianDay, settings);
    }

    /// <summary>
    /// Calculate positions for primary planets (Sun through Pluto)
    /// </summary>
    private void CalculatePrimaryPlanets(NatalChart chart, double julianDay, CalculationSettings settings)
    {
        var primaryPlanets = GetPrimaryPlanetDefinitions();

        foreach (var planetDef in primaryPlanets)
        {
            var position = CalculatePlanetPosition(julianDay, planetDef.SwissEphId, planetDef.PlanetType, settings);
            if (position != null)
            {
                chart.planets.Add(position);
            }
        }
    }

    /// <summary>
    /// Calculate positions for additional astrological points (Nodes, Chiron, etc.)
    /// </summary>
    private void CalculateAdditionalPoints(NatalChart chart, double julianDay, CalculationSettings settings)
    {
        var additionalPoints = GetAdditionalPointDefinitions();

        foreach (var pointDef in additionalPoints)
        {
            var position = CalculateAdditionalPoint(julianDay, pointDef.SwissEphId, pointDef.PlanetType, settings);
            if (position != null)
            {
                chart.planets.Add(position);
            }
        }
    }

    /// <summary>
    /// Calculate position for a single planet using Swiss Ephemeris
    /// </summary>
    private PlanetPosition CalculatePlanetPosition(double julianDay, int planetId, PlanetType planetType, CalculationSettings settings)
    {
        try
        {
            var positions = new double[6];
            string error = "";

            int result = swissEph.swe_calc_ut(julianDay, planetId, CALCULATION_FLAGS, positions, ref error);

            if (result < 0)
            {
                Debug.LogWarning($"Error calculating {planetType}: {error}");
                return null;
            }

            double longitude = ApplyCoordinateSystem(positions[0], settings);

            return CreatePlanetPosition(planetType, longitude, positions);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error calculating {planetType}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calculate position for additional astrological points with special handling
    /// </summary>
    private PlanetPosition CalculateAdditionalPoint(double julianDay, int seId, PlanetType pointType, CalculationSettings settings)
    {
        try
        {
            if (pointType == PlanetType.SouthNode)
            {
                return CalculateSouthNode(julianDay, settings);
            }
            else
            {
                return CalculatePlanetPosition(julianDay, seId, pointType, settings);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error calculating {pointType}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calculate South Node position as opposite of North Node
    /// </summary>
    private PlanetPosition CalculateSouthNode(double julianDay, CalculationSettings settings)
    {
        var northNode = CalculatePlanetPosition(julianDay, SwissEph.SE_TRUE_NODE, PlanetType.NorthNode, settings);
        if (northNode == null) return null;

        double southNodeLongitude = NormalizeAngle(northNode.longitude + 180.0);

        return new PlanetPosition
        {
            planetType = PlanetType.SouthNode,
            longitude = southNodeLongitude,
            latitude = -northNode.latitude,
            distance = northNode.distance,
            longitudeSpeed = -northNode.longitudeSpeed,
            latitudeSpeed = -northNode.latitudeSpeed,
            distanceSpeed = northNode.distanceSpeed,
            zodiacSign = GetZodiacSign(southNodeLongitude),
            degreeInSign = GetDegreeInSign(southNodeLongitude),
            isRetrograde = northNode.isRetrograde
        };
    }

    /// <summary>
    /// Apply coordinate system (tropical or sidereal) to longitude
    /// </summary>
    private double ApplyCoordinateSystem(double longitude, CalculationSettings settings)
    {
        if (!settings.UseTropical)
        {
            longitude -= settings.Ayanamsa;
        }

        return NormalizeAngle(longitude);
    }

    /// <summary>
    /// Create PlanetPosition object from calculated data
    /// </summary>
    private PlanetPosition CreatePlanetPosition(PlanetType planetType, double longitude, double[] positions)
    {
        return new PlanetPosition
        {
            planetType = planetType,
            longitude = longitude,
            latitude = positions[1],
            distance = positions[2],
            longitudeSpeed = positions[3],
            latitudeSpeed = positions[4],
            distanceSpeed = positions[5],
            zodiacSign = GetZodiacSign(longitude),
            degreeInSign = GetDegreeInSign(longitude),
            isRetrograde = positions[3] < 0
        };
    }

    #endregion

    #region HOUSE CALCULATIONS

    /// <summary>
    /// Calculate house cusps and chart angles
    /// </summary>
    private void CalculateHouses(NatalChart chart, double julianDay, float latitude, float longitude, HouseSystem houseSystem)
    {
        try
        {
            var houseData = CalculateHouseData(julianDay, latitude, longitude, houseSystem);

            if (houseSystem == HouseSystem.Equal)
            {
                CalculateEqualHouses(chart, houseData.Angles[0]); // Use Ascendant
            }
            else
            {
                CalculateStandardHouses(chart, houseData.Cusps);
            }

            CalculateAngles(chart, houseData.Angles);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error calculating houses: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate raw house data using Swiss Ephemeris
    /// </summary>
    private HouseCalculationData CalculateHouseData(double julianDay, float latitude, float longitude, HouseSystem houseSystem)
    {
        var cusps = new double[13]; // 12 houses + extra
        var angles = new double[10]; // Ascendant, MC, etc.
        char houseSystemChar = GetHouseSystemChar(houseSystem);

        int result = swissEph.swe_houses(julianDay, latitude, longitude, houseSystemChar, cusps, angles);

        if (result < 0)
        {
            throw new Exception("Swiss Ephemeris house calculation failed");
        }

        return new HouseCalculationData { Cusps = cusps, Angles = angles };
    }

    /// <summary>
    /// Calculate standard house system cusps (Koch, Placidus)
    /// </summary>
    private void CalculateStandardHouses(NatalChart chart, double[] cusps)
    {
        chart.houses = new List<HousePosition>();
        var settings = GetCalculationSettings();

        for (int i = 1; i <= 12; i++)
        {
            double houseCusp = ApplyCoordinateSystem(cusps[i], settings);

            chart.houses.Add(new HousePosition
            {
                houseNumber = i,
                cuspLongitude = houseCusp,
                zodiacSign = GetZodiacSign(houseCusp)
            });
        }
    }

    /// <summary>
    /// Calculate equal house system (30° divisions from Ascendant)
    /// </summary>
    private void CalculateEqualHouses(NatalChart chart, double ascendantLongitude)
    {
        var settings = GetCalculationSettings();
        ascendantLongitude = ApplyCoordinateSystem(ascendantLongitude, settings);

        chart.houses = new List<HousePosition>();

        for (int i = 1; i <= 12; i++)
        {
            double houseCusp = NormalizeAngle(ascendantLongitude + ((i - 1) * 30.0));

            chart.houses.Add(new HousePosition
            {
                houseNumber = i,
                cuspLongitude = houseCusp,
                zodiacSign = GetZodiacSign(houseCusp)
            });
        }
    }

    /// <summary>
    /// Calculate chart angles (Ascendant, Midheaven, etc.)
    /// </summary>
    private void CalculateAngles(NatalChart chart, double[] angles)
    {
        var settings = GetCalculationSettings();

        double ascendantLong = ApplyCoordinateSystem(angles[0], settings);
        double midheavenLong = ApplyCoordinateSystem(angles[1], settings);

        chart.ascendant = new AnglePosition
        {
            longitude = ascendantLong,
            zodiacSign = GetZodiacSign(ascendantLong)
        };

        chart.midheaven = new AnglePosition
        {
            longitude = midheavenLong,
            zodiacSign = GetZodiacSign(midheavenLong)
        };
    }

    #endregion

    #region UTILITY METHODS

    /// <summary>
    /// Get zodiac sign from ecliptic longitude
    /// </summary>
    private ZodiacSign GetZodiacSign(double longitude)
    {
        int signIndex = (int)(longitude / 30) % 12;
        return (ZodiacSign)signIndex;
    }

    /// <summary>
    /// Get degree position within zodiac sign
    /// </summary>
    private double GetDegreeInSign(double longitude)
    {
        return longitude % 30;
    }

    /// <summary>
    /// Get house system character for Swiss Ephemeris
    /// </summary>
    private char GetHouseSystemChar(HouseSystem system)
    {
        switch (system)
        {
            case HouseSystem.Koch: return 'K';
            case HouseSystem.Placidus: return 'P';
            case HouseSystem.Equal: return 'E';
            default: return 'K';
        }
    }

    /// <summary>
    /// Get house system display name
    /// </summary>
    private string GetHouseSystemDisplayName(HouseSystem system)
    {
        switch (system)
        {
            case HouseSystem.Koch: return "Koch";
            case HouseSystem.Placidus: return "Placidus";
            case HouseSystem.Equal: return "Equal";
            default: return "Koch";
        }
    }

    #endregion

    #region VALIDATION

    /// <summary>
    /// Validate preconditions for chart calculation
    /// </summary>
    private bool ValidateCalculationPreconditions(BirthData birthData)
    {
        if (swissEph == null)
        {
            HandleCalculationError("Swiss Ephemeris not initialized");
            return false;
        }

        if (birthData == null)
        {
            HandleCalculationError("Birth data is null");
            return false;
        }

        return true;
    }

    #endregion

    #region ERROR HANDLING

    /// <summary>
    /// Handle calculation errors with consistent logging and event notification
    /// </summary>
    private void HandleCalculationError(string message)
    {
        Debug.LogError($"PlanetCalculator: {message}");
        OnCalculationError?.Invoke(message);
    }

    #endregion

    #region PLANET DEFINITIONS

    /// <summary>
    /// Get primary planet definitions for calculation
    /// </summary>
    private List<PlanetDefinition> GetPrimaryPlanetDefinitions()
    {
        return new List<PlanetDefinition>
        {
            new PlanetDefinition { PlanetType = PlanetType.Sun, SwissEphId = SwissEph.SE_SUN },
            new PlanetDefinition { PlanetType = PlanetType.Moon, SwissEphId = SwissEph.SE_MOON },
            new PlanetDefinition { PlanetType = PlanetType.Mercury, SwissEphId = SwissEph.SE_MERCURY },
            new PlanetDefinition { PlanetType = PlanetType.Venus, SwissEphId = SwissEph.SE_VENUS },
            new PlanetDefinition { PlanetType = PlanetType.Mars, SwissEphId = SwissEph.SE_MARS },
            new PlanetDefinition { PlanetType = PlanetType.Jupiter, SwissEphId = SwissEph.SE_JUPITER },
            new PlanetDefinition { PlanetType = PlanetType.Saturn, SwissEphId = SwissEph.SE_SATURN },
            new PlanetDefinition { PlanetType = PlanetType.Uranus, SwissEphId = SwissEph.SE_URANUS },
            new PlanetDefinition { PlanetType = PlanetType.Neptune, SwissEphId = SwissEph.SE_NEPTUNE },
            new PlanetDefinition { PlanetType = PlanetType.Pluto, SwissEphId = SwissEph.SE_PLUTO }
        };
    }

    /// <summary>
    /// Get additional astrological point definitions for calculation
    /// </summary>
    private List<PlanetDefinition> GetAdditionalPointDefinitions()
    {
        return new List<PlanetDefinition>
        {
            new PlanetDefinition { PlanetType = PlanetType.NorthNode, SwissEphId = SwissEph.SE_TRUE_NODE },
            new PlanetDefinition { PlanetType = PlanetType.SouthNode, SwissEphId = SwissEph.SE_TRUE_NODE }, // Calculated as opposite
            new PlanetDefinition { PlanetType = PlanetType.Chiron, SwissEphId = SwissEph.SE_CHIRON },
            new PlanetDefinition { PlanetType = PlanetType.Lilith, SwissEphId = SwissEph.SE_MEAN_APOG },
            new PlanetDefinition { PlanetType = PlanetType.Proserpine, SwissEphId = SwissEph.SE_PROSERPINA }
        };
    }

    #endregion
}

#region HELPER STRUCTURES

/// <summary>
/// Planet definition linking astrological planet types to Swiss Ephemeris IDs
/// </summary>
internal struct PlanetDefinition
{
    public PlanetType PlanetType;
    public int SwissEphId;
}

/// <summary>
/// Calculation settings container
/// </summary>
internal struct CalculationSettings
{
    public bool UseTropical;
    public HouseSystem HouseSystem;
    public float Ayanamsa;
}

/// <summary>
/// House calculation data container
/// </summary>
internal struct HouseCalculationData
{
    public double[] Cusps;
    public double[] Angles;
}

#endregion

#region DATA STRUCTURES

/// <summary>
/// Complete natal chart data structure containing all astrological elements
/// </summary>
[System.Serializable]
public class NatalChart
{
    public BirthData birthData;
    public double julianDay;
    public float ayanamsa;
    public List<PlanetPosition> planets;
    public List<HousePosition> houses;
    public AnglePosition ascendant;
    public AnglePosition midheaven;
}

/// <summary>
/// Individual planet position data with astronomical coordinates and astrological properties
/// </summary>
[System.Serializable]
public class PlanetPosition
{
    public PlanetType planetType;
    public double longitude;
    public double latitude;
    public double distance;
    public double longitudeSpeed;
    public double latitudeSpeed;
    public double distanceSpeed;
    public ZodiacSign zodiacSign;
    public double degreeInSign;
    public bool isRetrograde;
}

/// <summary>
/// House cusp position data for astrological house systems
/// </summary>
[System.Serializable]
public class HousePosition
{
    public int houseNumber;
    public double cuspLongitude;
    public ZodiacSign zodiacSign;
}

/// <summary>
/// Angle position data for chart angles (Ascendant, Midheaven, etc.)
/// </summary>
[System.Serializable]
public class AnglePosition
{
    public double longitude;
    public ZodiacSign zodiacSign;
}

/// <summary>
/// Planet type enumeration for astrological calculations
/// </summary>
public enum PlanetType
{
    Sun = 0,
    Moon = 1,
    Mercury = 2,
    Venus = 3,
    Mars = 4,
    Jupiter = 5,
    Saturn = 6,
    Uranus = 7,
    Neptune = 8,
    Pluto = 9,
    NorthNode = 10,
    SouthNode = 11,
    Chiron = 12,
    Lilith = 13,
    Proserpine = 14
}

/// <summary>
/// Zodiac sign enumeration in standard astrological order
/// </summary>
public enum ZodiacSign
{
    Aries = 0,
    Taurus = 1,
    Gemini = 2,
    Cancer = 3,
    Leo = 4,
    Virgo = 5,
    Libra = 6,
    Scorpio = 7,
    Sagittarius = 8,
    Capricorn = 9,
    Aquarius = 10,
    Pisces = 11
}

#endregion