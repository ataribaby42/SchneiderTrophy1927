using Microsoft.FlightSimulator.SimConnect;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

class Program
{
    // SimConnect instance
    static SimConnect simconnect = null!;

    public class TimeData
    {
        public TimeSpan FastestLap1 { get; set; }
        public TimeSpan FastestRace1 { get; set; }
        public TimeSpan FastestLap2 { get; set; }
        public TimeSpan FastestRace2 { get; set; }
        public TimeSpan FastestLap3 { get; set; }
        public TimeSpan FastestRace3 { get; set; }
    }

    static private TimeData times = new TimeData();
    static private string filePath = "times.json";

    static void Save(TimeData data)
    {
        try
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch { }
    }
    static TimeData Load()
    {
        try
        {
            if (!File.Exists(filePath))
                return new TimeData(); // return defaults if no file

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<TimeData>(json);
        }
        catch 
        {
            return new TimeData();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    struct EngineAndPositionStruct
    {
        public double EngineRPM; // Engine RPM
        public double OilTemp; // Oil Temperature (Celsius)
        public double OilPressure; // Oil Pressure (PSI)
        public double RadiatorTemp; // Coolant (Radiator) Temperature (Celsius)
        public double Latitude; // Aircraft Latitude
        public double Longitude; // Aircraft Longitude
        public double Altitude; // Aircraft Altitude (feet)
        public double LocalTime; // seconds since midnight local sim time
    }

    enum DEFINITIONS 
    { 
        DataStruct
    }

    enum DATA_REQUESTS 
    { 
        DataRequest
    }

    enum EVENTS
    {
        TOGGLE_ENGINE1_FAILURE,
    }

    enum SIMCONNECT_GROUP_PRIORITY : Int64
    {
        HIGHEST = 1,
        MASKABLE = 10000000,
        STANDARD = 1900000000,
        DEFAULT = 2000000000,
        LOWEST = 4000000000,
    }

    enum RacingPointType
    {
        Start,
        Finish,
        Turn
    }

    struct RacingPoint
    {
        public string Name;
        public RacingPointType Type;
        public double X;
        public double Y;
        public double Distance;
        public double AngleMin;
        public double AngleMax;
    }

    static string[] CourseNames = {
        "SHORT",
        "MEDIUM",
        "FULL"
    };

    static RacingPoint[] trackTemplate = {
        new() {
            Name = "Start - Hotel Excelsior",
            Type = RacingPointType.Start,
            X = 45.400642,
            Y = 12.372607,
            Distance = 0.004976,
            AngleMax = -64,
            AngleMin = -80
        },
        new() {
            Name = "Turnpoint - Alberoni lighthouse",
            Type = RacingPointType.Turn,
            X = 45.333908,
            Y = 12.343131,
            Distance = 0.014267,
            AngleMax = -45,
            AngleMin = -97
        },
        new() {
            Name = "Turnpoint - Chioggia lighthouse",
            Type = RacingPointType.Turn,
            X = 45.228628,
            Y = 12.313616,
            Distance = 0.014267,
            AngleMax = -116,
            AngleMin = 145
        },
        new() {
            Name = "Turnpoint - San Nicolo lighthouse",
            Type = RacingPointType.Turn,
            X = 45.417894,
            Y = 12.427183,
            Distance = 0.014267,
            AngleMax = 102,
            AngleMin = 34
        },
        new() {
            Name = "Finish - Hotel Excelsior",
            Type = RacingPointType.Finish,
            X = 45.400642,
            Y = 12.372607,
            Distance = 0.004976,
            AngleMax = -64,
            AngleMin = -80
        },
    };

    static RacingPoint[] track = new RacingPoint[0];

    static double maxWaterTemp = 95d;
    static double maxOilTemp = 140d;
    static double maxRPM = 3300d;
    static double lastTime = -1;
    static double time = 0;
    static int trackPosition = 0;
    static int lap = 0;
    static double lapTimeStart = 0;
    static double raceTime = 0;
    static double maxEngineFailureTime = 60;
    static double engineFailureTime = 0;
    static bool engineFailure = false;
    static bool practice = true;
    static int raceLaps = 7;
    static Random dice = new Random();
    static bool sound = true;
    static int layout = 3;

    static char ReadValidKey(char[] validKeys)
    {
        while (true)
        {
            var key = Console.ReadKey(true).KeyChar;
            
            if (Array.Exists(validKeys, k => char.ToUpper(k) == char.ToUpper(key)))
            {
                if (char.ToUpper(key) == 'C')
                {
                    layout++;
                    
                    if (layout > 3)
                    {
                        layout = 1;
                    }
                    
                    Menu();
                    continue;
                }
                if (char.ToUpper(key) == 'L')
                {
                    raceLaps++;

                    if (raceLaps > 10)
                    {
                        raceLaps = 1;
                    }

                    Menu();
                    continue;
                }
                else if (char.ToUpper(key) == 'S')
                {
                    sound = !sound;
                    Menu();
                    continue;
                }
                else
                {
                    return key;
                }
            }

            Console.Beep(); // feedback for invalid key
        }
    }

    static void UpdateTrack()
    {
        List<RacingPoint> newTrack = new List<RacingPoint>();
        newTrack.Add(trackTemplate[0]);

        if (layout == 1)
        {
            newTrack.Add(trackTemplate[3]);
        }
        else if (layout == 2)
        {
            newTrack.Add(trackTemplate[1]);
            newTrack.Add(trackTemplate[3]);
        }
        else if (layout == 3)
        {
            newTrack.Add(trackTemplate[1]);
            newTrack.Add(trackTemplate[2]);
            newTrack.Add(trackTemplate[3]);
        }

        newTrack.Add(trackTemplate[4]);
        track = newTrack.ToArray();
    }

    static private void Menu()
    {
        Console.Clear();
        Console.WriteLine("Schneider Trophy 1927 - Supermarine S.5 Editon");
        Console.WriteLine("2025 Ataribaby v1.0");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Press P to begin Practice..");
        Console.WriteLine("Press R to begin Race...");
        Console.WriteLine($"Press C to change course layout [{CourseNames[layout - 1]}]...");
        //Console.WriteLine($"Press L to change race laps [{raceLaps}]...");
        Console.WriteLine($"Press S to switch sound [{(sound ? "ON" : "OFF")}]...");
        Console.WriteLine("Press Q to exit...");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Short Course Records");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Fastest Lap Time: {DisplayTimeSpan(times.FastestLap1).ToString(@"hh\:mm\:ss\.ff")}");
        Console.WriteLine($"Fastest Race Time: {DisplayTimeSpan(times.FastestRace1).ToString(@"hh\:mm\:ss\.ff")}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Medium Course Records");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Fastest Lap Time: {DisplayTimeSpan(times.FastestLap2).ToString(@"hh\:mm\:ss\.ff")}");
        Console.WriteLine($"Fastest Race Time: {DisplayTimeSpan(times.FastestRace2).ToString(@"hh\:mm\:ss\.ff")}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Full Course Records");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Fastest Lap Time: {DisplayTimeSpan(times.FastestLap3).ToString(@"hh\:mm\:ss\.ff")}");
        Console.WriteLine($"Fastest Race Time: {DisplayTimeSpan(times.FastestRace3).ToString(@"hh\:mm\:ss\.ff")}");
        Console.ResetColor();
    }

    private static TimeSpan DisplayTimeSpan(TimeSpan time)
    {
        if (time == TimeSpan.MaxValue)
        {
            return TimeSpan.Zero;
        }
        else
        {
            return time;
        }
    }

    static void Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        times = Load();

        if (times.FastestLap1 == TimeSpan.Zero)
        {
            times.FastestLap1 = TimeSpan.MaxValue;
            Save(times);
        }

        if (times.FastestRace1 == TimeSpan.Zero)
        {
            times.FastestRace1 = TimeSpan.MaxValue;
            Save(times);
        }

        if (times.FastestLap2 == TimeSpan.Zero)
        {
            times.FastestLap2 = TimeSpan.MaxValue;
            Save(times);
        }

        if (times.FastestRace2 == TimeSpan.Zero)
        {
            times.FastestRace2 = TimeSpan.MaxValue;
            Save(times);
        }

        if (times.FastestLap3 == TimeSpan.Zero)
        {
            times.FastestLap3 = TimeSpan.MaxValue;
            Save(times);
        }

        if (times.FastestRace3 == TimeSpan.Zero)
        {
            times.FastestRace3 = TimeSpan.MaxValue;
            Save(times);
        }

        Menu();
        //char choice = ReadValidKey(new[] { 'P', 'R', 'C', 'L', 'S', 'Q' });
        char choice = ReadValidKey(new[] { 'P', 'R', 'C','S', 'Q' });
        Reset();

        if (char.ToUpper(choice) == 'P')
        {
            practice = true;
        }
        else if (char.ToUpper(choice) == 'R')
        {
            practice = false;
        }
        else if (char.ToUpper(choice) == 'Q')
        {
            Console.Clear();
            Environment.Exit(0);
        }

        Console.Clear();
        Console.WriteLine("Connecting to MSFS...");

        try
        {
            simconnect = new SimConnect("MSFSPositionReader", IntPtr.Zero, 0, null, 0);

            // Define data structure
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "GENERAL ENG RPM:1", "rpm", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "GENERAL ENG OIL TEMPERATURE:1", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "GENERAL ENG OIL PRESSURE:1", "psi", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "RECIP ENG RADIATOR TEMPERATURE:1", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            simconnect.AddToDataDefinition(DEFINITIONS.DataStruct, "LOCAL TIME", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

            simconnect.RegisterDataDefineStruct<EngineAndPositionStruct>(DEFINITIONS.DataStruct);

            // Subscribe to events
            simconnect.OnRecvOpen += Simconnect_OnRecvOpen;
            simconnect.OnRecvQuit += Simconnect_OnRecvQuit;
            simconnect.OnRecvSimobjectData += Simconnect_OnRecvSimobjectData;

            simconnect.MapClientEventToSimEvent(EVENTS.TOGGLE_ENGINE1_FAILURE, "TOGGLE_ENGINE1_FAILURE");

            // Request data every sim frame
            simconnect.RequestDataOnSimObject(DATA_REQUESTS.DataRequest, DEFINITIONS.DataStruct, SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SIM_FRAME, SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT, 0, 0, 0);

            while (true)
            {
                simconnect.ReceiveMessage(); // Process SimConnect messages

                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                {
                    break;
                }

                Thread.Sleep(10);
            }

            simconnect.Dispose();
        }
        catch
        {
            Console.WriteLine("Unable to connect to Flight Simulator!");
        }
    }

    private static string GetTrackLayoutNames()
    {
        string layoutNames = string.Empty;

        for (int i = 0; i < track.Count(); i++)
        {
            layoutNames += track[i].Name;
            
            if (i < track.Count() - 1)
            {
                layoutNames += " | ";
            }
        }

        return layoutNames;
    }

    private static void Simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
    {
        Console.WriteLine("Connected to Flight Simulator!");
        Console.WriteLine();

        if (practice)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Practice started! Press ESC to exit..");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Course Layout [{CourseNames[layout - 1]}]: {GetTrackLayoutNames()}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Race started! Press ESC to exit..");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine($"Course Layout [{CourseNames[layout - 1]}]: {GetTrackLayoutNames()}");
            Console.WriteLine($"Race laps: {raceLaps}");
        }

        Console.WriteLine();
    }

    private static void Simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        Console.WriteLine("Flight Simulator has exited.");
        Environment.Exit(0);
    }

    private static void Simconnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)DATA_REQUESTS.DataRequest)
        {
            var simData = (EngineAndPositionStruct)data.dwData[0];
            double current = simData.LocalTime;
            double deltaTime = 0;

            if (lastTime >= 0)
            {
                deltaTime = current - lastTime;
                
                if (deltaTime < -43200)
                {
                    deltaTime += 86400; // Adjust for crossing midnight
                }
                
                time += deltaTime;
            }

            lastTime = current;

            if ((practice || !practice && lap < raceLaps) && CheckRacingPoint(track[trackPosition], simData.Latitude, simData.Longitude, simData.Altitude))
            {
                Console.WriteLine($"Passed: {track[trackPosition].Name}");

                if (sound)
                {
                    if (track[trackPosition].Type == RacingPointType.Start && lap == 0)
                    {
                        Console.Beep();
                        Console.Beep();
                    }
                    else if (track[trackPosition].Type == RacingPointType.Start && practice)
                    {
                        Console.Beep();
                    }
                    else if (track[trackPosition].Type == RacingPointType.Finish)
                    {
                        Console.Beep();
                    }
                    else if (track[trackPosition].Type == RacingPointType.Turn)
                    {
                        Console.Beep();
                    }
                }

                if (track[trackPosition].Type == RacingPointType.Start)
                {
                    lapTimeStart = time;
                }

                trackPosition++;

                if (trackPosition >= track.Length)
                {
                    trackPosition = 0;
                    lap++;
                    double lapTime = time - lapTimeStart;
                    raceTime += lapTime;
                    Console.ForegroundColor = ConsoleColor.Green;
                    TimeSpan lapTimeSpan = TimeSpan.FromSeconds(lapTime).RoundTo(TimeSpan.FromMilliseconds(10));
                    Console.WriteLine($"Lap: {lap} {lapTimeSpan.ToString(@"hh\:mm\:ss\.ff")}");
                    Console.ForegroundColor = ConsoleColor.Yellow;

                    if (layout == 1)
                    {
                        if (lapTimeSpan < times.FastestLap1)
                        {
                            Console.WriteLine("New Lap Record!");
                            times.FastestLap1 = lapTimeSpan;
                            Save(times);
                        }
                    }
                    else if (layout == 2)
                    {
                        if (lapTimeSpan < times.FastestLap2)
                        {
                            Console.WriteLine("New Lap Record!");
                            times.FastestLap2 = lapTimeSpan;
                            Save(times);
                        }
                    }
                    else if (layout == 3)
                    {
                        if (lapTimeSpan < times.FastestLap3)
                        {
                            Console.WriteLine("New Lap Record!");
                            times.FastestLap3 = lapTimeSpan;
                            Save(times);
                        }
                    }

                    Console.ResetColor();

                    if (!practice && lap == raceLaps)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        TimeSpan raceTimeSpan = TimeSpan.FromSeconds(raceTime).RoundTo(TimeSpan.FromMilliseconds(10));
                        Console.WriteLine($"Race finished! {raceTimeSpan.ToString(@"hh\:mm\:ss\.ff")}");
                        Console.ForegroundColor = ConsoleColor.Yellow;

                        if (layout == 1)
                        {
                            if (raceTimeSpan < times.FastestRace1)
                            {
                                Console.WriteLine("New Race Record!");
                                times.FastestRace1 = raceTimeSpan;
                                Save(times);
                            }
                        }
                        if (layout == 2)
                        {
                            if (raceTimeSpan < times.FastestRace2)
                            {
                                Console.WriteLine("New Race Record!");
                                times.FastestRace2 = raceTimeSpan;
                                Save(times);
                            }
                        }
                        if (layout == 3)
                        {
                            if (raceTimeSpan < times.FastestRace3)
                            {
                                Console.WriteLine("New Race Record!");
                                times.FastestRace3 = raceTimeSpan;
                                Save(times);
                            }
                        }

                        Console.ResetColor();
                        
                        if (sound)
                        {
                            Console.Beep();
                        }
                    }
                }
            }

            RunEngineDamage(deltaTime, simData.EngineRPM, simData.OilTemp, simData.RadiatorTemp);
        }
    }

    private static void Reset()
    {
        engineFailure = false;
        engineFailureTime = 0;
        maxEngineFailureTime = dice.Next(50, 60);
        trackPosition = 0;
        raceTime = 0;
        lap = 0;
        lastTime = -1;
        lapTimeStart = 0;
        time = 0;
        UpdateTrack();
    }

    private static void RunEngineDamage(double deltaTime, double RPM, double oilTemp, double waterTemp)
    {
        if (engineFailure)
        {
            return;
        }

        double buffer = 100d;

        if (RPM > maxRPM)
        {
            // Normalize: 0 at maxRPM, 1 at maxRPM + buffer
            double scale = Math.Clamp((RPM - maxRPM) / buffer, 0.0, 1.0);
            engineFailureTime += scale * deltaTime;
        }

        buffer = 10d;

        if (oilTemp > maxOilTemp)
        {
            // Normalize: 0 at maxOilTemp, 1 at maxOilTemp + buffer
            double scale = Math.Clamp((oilTemp - maxOilTemp) / buffer, 0.0, 1.0);
            engineFailureTime += scale * deltaTime;
        }

        buffer = 10d;

        if (waterTemp > maxWaterTemp)
        {
            // Normalize: 0 at maxWaterTemp, 1 at maxWaterTemp + buffer
            double scale = Math.Clamp((waterTemp - maxWaterTemp) / buffer, 0.0, 1.0);
            engineFailureTime += scale * deltaTime;
        }

        if (engineFailureTime >= maxEngineFailureTime)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Engine Failure!");
            Console.ResetColor();
            engineFailure = true;
            simconnect.TransmitClientEvent(
                0,  // Object ID 0 = player aircraft
                EVENTS.TOGGLE_ENGINE1_FAILURE,
                0,  // Data (not used for this toggle)
                SIMCONNECT_GROUP_PRIORITY.HIGHEST,
                SIMCONNECT_EVENT_FLAG.GROUPID_IS_PRIORITY
            );
        }

    }

    private static double Distance(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double Angle(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        double angleRadians = Math.Atan2(dy, dx);
        return angleRadians * (180.0 / Math.PI);
    }

    private static bool IsAngleInRange(double angle, double min, double max)
    {
        // Normalize to -180..180
        angle = ((angle + 180d) % 360d + 360d) % 360d - 180d;
        min = ((min + 180d) % 360d + 360d) % 360d - 180d;
        max = ((max + 180d) % 360d + 360d) % 360d - 180d;

        // Handle wrap-around
        if (min <= max)
            return angle >= min && angle <= max;
        else
            return angle >= min || angle <= max;
    }

    private static bool CheckRacingPoint(RacingPoint racingPoint, double x, double y, double alt)
    {
        if ((racingPoint.Type == RacingPointType.Start || racingPoint.Type == RacingPointType.Finish) && alt < 20d)
        {
            return false;
        }

        if (Distance(racingPoint.X, racingPoint.Y, x, y) < racingPoint.Distance)
        {
            double angle = Angle(racingPoint.X, racingPoint.Y, x, y);

            if (IsAngleInRange(angle, racingPoint.AngleMin, racingPoint.AngleMax))
            {
                return true;
            }
        }

        return false;
    }
}

