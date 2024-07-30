using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using SDL;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using static SDL.SDL3;

namespace Ryujinx.SDL2.Common
{
    public unsafe class SDL2Driver : IDisposable
    {
        private static SDL2Driver _instance;

        public static SDL2Driver Instance
        {
            get
            {
                _instance ??= new SDL2Driver();

                return _instance;
            }
        }

        public static Action<Action> MainThreadDispatcher { get; set; }

        private const SDL_InitFlags SdlInitFlags = SDL_InitFlags.SDL_INIT_EVENTS | SDL_InitFlags.SDL_INIT_GAMEPAD | SDL_InitFlags.SDL_INIT_JOYSTICK | SDL_InitFlags.SDL_INIT_AUDIO | SDL_InitFlags.SDL_INIT_VIDEO;

        private bool _isRunning;
        private uint _refereceCount;
        private Thread _worker;

        public event Action<SDL_JoystickID> OnJoyStickConnected;
        public event Action<SDL_JoystickID> OnJoystickDisconnected;

        private ConcurrentDictionary<SDL_WindowID, Action<SDL_Event>> _registeredWindowHandlers;

        private readonly object _lock = new();

        private SDL2Driver() { }

        private const string SDL_HINT_JOYSTICK_HIDAPI_COMBINE_JOY_CONS = "SDL_JOYSTICK_HIDAPI_COMBINE_JOY_CONS";

        public void Initialize()
        {
            lock (_lock)
            {
                _refereceCount++;

                if (_isRunning)
                {
                    return;
                }

                SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_PS4_RUMBLE, "1");
                SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_PS5_RUMBLE, "1");
                SDL_SetHint(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");
                SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_SWITCH_HOME_LED, "0");
                SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_JOY_CONS, "1");
                SDL_SetHint(SDL_HINT_VIDEO_ALLOW_SCREENSAVER, "1");


                // NOTE: As of SDL2 2.24.0, joycons are combined by default but the motion source only come from one of them.
                // We disable this behavior for now.
                SDL_SetHint(SDL_HINT_JOYSTICK_HIDAPI_COMBINE_JOY_CONS, "0");

                if (SDL_Init(SdlInitFlags) != 0)
                {
                    string errorMessage = $"SDL2 initialization failed with error \"{SDL_GetError()}\"";

                    Logger.Error?.Print(LogClass.Application, errorMessage);

                    throw new Exception(errorMessage);
                }

                // First ensure that we only enable joystick events (for connected/disconnected).
                SDL_SetGamepadEventsEnabled(SDL_bool.SDL_FALSE);
                SDL_SetJoystickEventsEnabled(SDL_bool.SDL_TRUE);

                // Disable all joysticks information, we don't need them no need to flood the event queue for that.
                SDL_SetEventEnabled(SDL_EventType.SDL_EVENT_JOYSTICK_AXIS_MOTION, SDL_bool.SDL_FALSE);
                SDL_SetEventEnabled(SDL_EventType.SDL_EVENT_JOYSTICK_BALL_MOTION, SDL_bool.SDL_FALSE);
                SDL_SetEventEnabled(SDL_EventType.SDL_EVENT_JOYSTICK_HAT_MOTION, SDL_bool.SDL_FALSE);
                SDL_SetEventEnabled(SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_DOWN, SDL_bool.SDL_FALSE);
                SDL_SetEventEnabled(SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_UP, SDL_bool.SDL_FALSE);

                SDL_SetEventEnabled(SDL_EventType.SDL_EVENT_GAMEPAD_SENSOR_UPDATE, SDL_bool.SDL_FALSE);

                string gamepadDbPath = Path.Combine(AppDataManager.BaseDirPath, "SDL_GameControllerDB.txt");

                if (File.Exists(gamepadDbPath))
                {
                    SDL_AddGamepadMappingsFromFile(gamepadDbPath);
                }

                _registeredWindowHandlers = new ConcurrentDictionary<SDL_WindowID, Action<SDL_Event>>();
                _worker = new Thread(EventWorker);
                _isRunning = true;
                _worker.Start();
            }
        }

        public bool RegisterWindow(SDL_WindowID windowId, Action<SDL_Event> windowEventHandler)
        {
            return _registeredWindowHandlers.TryAdd(windowId, windowEventHandler);
        }

        public void UnregisterWindow(SDL_WindowID windowId)
        {
            _registeredWindowHandlers.Remove(windowId, out _);
        }

        private void HandleSDLEvent(ref SDL_Event evnt)
        {
            if (evnt.Type == SDL_EventType.SDL_EVENT_JOYSTICK_ADDED)
            {
                OnJoyStickConnected?.Invoke(evnt.gbutton.which);
            }
            else if (evnt.Type == SDL_EventType.SDL_EVENT_JOYSTICK_REMOVED)
            {
                Logger.Debug?.Print(LogClass.Application, $"Removed joystick instance id {evnt.gbutton.which}");

                OnJoystickDisconnected?.Invoke(evnt.gbutton.which);
            }
            else if (evnt.Type is >= SDL_EventType.SDL_EVENT_WINDOW_FIRST and <= SDL_EventType.SDL_EVENT_WINDOW_LAST or SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN or SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP)
            {
                if (_registeredWindowHandlers.TryGetValue(evnt.window.windowID, out Action<SDL_Event> handler))
                {
                    handler(evnt);
                }
            }
        }

        private void EventWorker()
        {
            const int WaitTimeMs = 10;

            using ManualResetEventSlim waitHandle = new(false);

            while (_isRunning)
            {
                MainThreadDispatcher?.Invoke(() =>
                {
                    SDL_Event evnt = new();
                    SDL_Event* evntPtr = &evnt;

                    while (SDL_PollEvent(evntPtr) != 0)
                    {
                        HandleSDLEvent(ref evnt);
                    }
                });

                waitHandle.Wait(WaitTimeMs);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            lock (_lock)
            {
                if (_isRunning)
                {
                    _refereceCount--;

                    if (_refereceCount == 0)
                    {
                        _isRunning = false;

                        _worker?.Join();

                        SDL_Quit();

                        OnJoyStickConnected = null;
                        OnJoystickDisconnected = null;
                    }
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }
    }
}
