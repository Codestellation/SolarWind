``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
AMD Ryzen 7 3700X, 1 CPU, 16 logical and 8 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4150.0), X64 RyuJIT
  Job-PXRZLC : .NET Framework 4.8 (4.8.4150.0), X64 RyuJIT
  Job-OOBLRO : .NET Core 2.1.16 (CoreCLR 4.6.28516.03, CoreFX 4.6.28516.10), X64 RyuJIT
  Job-XEIULX : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT

InvocationCount=1  UnrollFactor=1  

```
|                  Method |       Runtime | MessageSize |       Mean |    Error |    StdDev |     Median |       Gen 0 |      Gen 1 |     Gen 2 |   Allocated |
|------------------------ |-------------- |------------ |-----------:|---------:|----------:|-----------:|------------:|-----------:|----------:|------------:|
| **Run_ping_pong_benchmark** |    **.NET 4.7.2** |          **64** |   **647.1 ms** | **38.88 ms** | **114.64 ms** |   **589.4 ms** |  **25000.0000** |  **7000.0000** | **2000.0000** |  **62163888 B** |
| Run_ping_pong_benchmark | .NET Core 2.1 |          64 |   406.9 ms | 12.05 ms |  31.95 ms |   404.0 ms |   6000.0000 |  1000.0000 |         - |           - |
| Run_ping_pong_benchmark | .NET Core 3.1 |          64 |   399.5 ms |  8.50 ms |  24.53 ms |   402.3 ms |   3000.0000 |  1000.0000 |         - |  26687064 B |
| **Run_ping_pong_benchmark** |    **.NET 4.7.2** |         **128** |   **582.5 ms** | **11.66 ms** |  **34.20 ms** |   **582.8 ms** |  **31000.0000** |  **6000.0000** | **1000.0000** |  **71485312 B** |
| Run_ping_pong_benchmark | .NET Core 2.1 |         128 |   416.1 ms |  8.29 ms |  23.52 ms |   413.9 ms |  11000.0000 |  2000.0000 |         - |           - |
| Run_ping_pong_benchmark | .NET Core 3.1 |         128 |   414.4 ms |  8.84 ms |  26.06 ms |   412.1 ms |   3000.0000 |  1000.0000 |         - |  27439904 B |
| **Run_ping_pong_benchmark** |    **.NET 4.7.2** |         **256** |   **684.3 ms** | **17.28 ms** |  **49.01 ms** |   **689.3 ms** |  **27000.0000** |  **5000.0000** |         **-** |  **80072472 B** |
| Run_ping_pong_benchmark | .NET Core 2.1 |         256 |   534.3 ms | 20.04 ms |  59.09 ms |   525.5 ms |  11000.0000 |  3000.0000 |         - |           - |
| Run_ping_pong_benchmark | .NET Core 3.1 |         256 |   593.2 ms | 21.04 ms |  62.05 ms |   603.2 ms |  10000.0000 |  4000.0000 | 1000.0000 |  81047504 B |
| **Run_ping_pong_benchmark** |    **.NET 4.7.2** |         **512** |   **946.3 ms** | **18.50 ms** |  **30.39 ms** |   **940.4 ms** |  **59000.0000** | **11000.0000** | **1000.0000** | **222448264 B** |
| Run_ping_pong_benchmark | .NET Core 2.1 |         512 |   856.0 ms | 16.91 ms |  36.75 ms |   857.3 ms |  38000.0000 | 11000.0000 | 1000.0000 |           - |
| Run_ping_pong_benchmark | .NET Core 3.1 |         512 |   856.9 ms | 17.05 ms |  44.62 ms |   868.7 ms |  22000.0000 |  9000.0000 | 1000.0000 | 179573312 B |
| **Run_ping_pong_benchmark** |    **.NET 4.7.2** |        **1024** | **1,358.9 ms** | **26.43 ms** |  **41.15 ms** | **1,371.0 ms** |  **90000.0000** | **26000.0000** | **1000.0000** | **376211992 B** |
| Run_ping_pong_benchmark | .NET Core 2.1 |        1024 | 1,190.5 ms | 23.26 ms |  30.25 ms | 1,188.1 ms |  72000.0000 | 21000.0000 | 1000.0000 |           - |
| Run_ping_pong_benchmark | .NET Core 3.1 |        1024 | 1,227.8 ms | 24.22 ms |  33.95 ms | 1,219.8 ms |  40000.0000 | 18000.0000 | 1000.0000 | 312638512 B |
| **Run_ping_pong_benchmark** |    **.NET 4.7.2** |        **2048** | **2,936.8 ms** | **75.17 ms** | **215.67 ms** | **2,876.0 ms** | **173000.0000** | **68000.0000** | **9000.0000** | **962726992 B** |
| Run_ping_pong_benchmark | .NET Core 2.1 |        2048 | 2,172.1 ms | 40.60 ms |  39.88 ms | 2,176.7 ms | 133000.0000 | 39000.0000 | 5000.0000 |           - |
| Run_ping_pong_benchmark | .NET Core 3.1 |        2048 | 2,105.9 ms | 42.12 ms |  59.04 ms | 2,104.1 ms |  71000.0000 | 34000.0000 | 4000.0000 | 516537104 B |
