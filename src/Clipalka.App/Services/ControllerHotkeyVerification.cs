using System.Text.Json;
using System.Windows.Threading;
using Clipalka.Core.Models;

namespace Clipalka.App.Services;

internal static class ControllerHotkeyVerification
{
    public static string Run()
    {
        var source = new FixtureController();
        IReadOnlyList<IControllerInput> devices = [source];
        var now = TimeSpan.Zero;
        var saves = 0;
        var allowed = true;
        var rejected = 0;
        ControllerBinding? learned = null;
        using var service = new ControllerHotkeyService(Dispatcher.CurrentDispatcher, () => allowed,
            () => saves++, () => devices, () => now, false, () => "replay выключен");
        service.BindingCaptured += value => learned = value;
        service.TriggerRejected += _ => rejected++;
        service.RefreshDevices();
        service.StartLearning(source.Id);
        void Read(int ms, params int[] inputs)
        {
            now = TimeSpan.FromMilliseconds(ms);
            source.Pressed = inputs;
            service.Poll();
        }
        Read(0);
        Read(20, 4);
        Read(40, 4, 8);
        Read(60);
        Require(learned is not null && learned.Inputs.SequenceEqual([4, 8]), "button learning");
        // Use the same persisted binding contract as a saved settings file.
        var restored = JsonSerializer.Deserialize<ControllerBinding>(JsonSerializer.Serialize(learned))!;
        service.Configure(restored);
        Read(80, 2);
        Read(100, 2, 4, 8);
        Require(saves == 1, "a short chord with another gameplay button held");
        Read(150, 2, 4, 8);
        Require(saves == 1, "one replay per press");
        Read(180, 2);
        Read(200, 2, 4, 8);
        Require(saves == 2, "rearm without releasing unrelated gameplay input");
        Read(220, 2);
        allowed = false;
        Read(240, 2, 4, 8);
        Require(saves == 2 && rejected == 1, "visible rejection instead of a silent blocked command");
        allowed = true;
        Read(300, 2, 4, 8);
        Require(saves == 2, "held chord cannot fire when permission returns");
        Read(320, 2);
        Read(340, 2, 4, 8);
        Require(saves == 3, "press after permission returns");
        devices = [];
        service.RefreshDevices();
        devices = [source];
        service.RefreshDevices();
        Read(400, 2, 4, 8);
        Require(saves == 3, "no phantom replay after reconnect");
        Read(420);
        Read(440, 4, 8);
        Require(saves == 4, "reconnect and new press");
        return "PASS: real controller service learns, restores binding and dispatches replay; short press, extra gameplay input, once-per-press, blocked feedback and reconnect verified. Physical Bluetooth input still needs a connected controller.";
    }

    private static void Require(bool value, string scenario)
    {
        if (!value) throw new InvalidOperationException("Controller pipeline failed: " + scenario);
    }

    private sealed class FixtureController : IControllerInput
    {
        public string Id => "fixture:controller";
        public string Name => "Controller verification";
        public int[] Pressed { get; set; } = [];
        public int[] ReadPressed() => Pressed;
        public string Label(int input) => "Button " + input;
    }
}
