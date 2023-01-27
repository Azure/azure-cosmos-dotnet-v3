﻿using CSharpProd.Features;
using CSharpProd.Features.CliArgs;
using CSharpProd.Features.CustomSettings;
using CSharpProd.HelloWorld;
using CSharpProd.HelloWorld.LoadSimulation;
using CSharpProd.HTTP;
using CSharpProd.MQTT;
using NBomber;

// -------------------------------
// -----Hello World examples -----
// -------------------------------
// new HelloWorldExample().Run();
new ScenarioWithInit().Run();
// new ScenarioWithSteps().Run();
// new StepsShareData().Run();

// new ParallelScenarios().Run();
// new ScenarioInjectRate().Run();
// new ScenarioKeepConstant().Run();
// new DelayedScenarioStart().Run();

// new ScenarioWithTimeout().Run();
// new ScenarioWithStepRetry().Run();
// new EmptyScenario().Run();

// ------------------
// ---- Features ----
// ------------------
// new DataFeedExample().Run();
// new CustomSettingsExample().Run();
// new ClientPoolMqttExample().Run();
// new CliArgsExample().Run();

// ----------------
// ----- HTTP -----
// ----------------
// new SimpleHttpExample().Run();
// new SequentialHttpSteps().Run();
// new HttpResponseValidation().Run();

// ----------------
// ----- MQTT -----
// ----------------
// new PingPongMqttTest().Run();
// new ClientPoolMqttExample().Run();
