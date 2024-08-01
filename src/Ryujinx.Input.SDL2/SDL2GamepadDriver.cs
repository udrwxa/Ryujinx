using Ryujinx.SDL2.Common;
using SDL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static SDL.SDL3;

namespace Ryujinx.Input.SDL2
{
    public class SDL2GamepadDriver : IGamepadDriver
    {
        private readonly Dictionary<SDL_JoystickID, string> _gamepadsInstanceIdsMapping;
        private readonly List<string> _gamepadsIds;
        private readonly object _lock = new object();

        public ReadOnlySpan<string> GamepadsIds
        {
            get
            {
                lock (_lock)
                {
                    return _gamepadsIds.ToArray();
                }
            }
        }

        public string DriverName => "SDL3";

        public event Action<string> OnGamepadConnected;
        public event Action<string> OnGamepadDisconnected;

        public SDL2GamepadDriver()
        {
            _gamepadsInstanceIdsMapping = new Dictionary<SDL_JoystickID, string>();
            _gamepadsIds = new List<string>();

            SDL2Driver.Instance.Initialize();
            SDL2Driver.Instance.OnJoyStickConnected += HandleJoyStickConnected;
            SDL2Driver.Instance.OnJoystickDisconnected += HandleJoyStickDisconnected;

            // Add already connected gamepads
            var joysticks = SDL_GetJoysticks();

            for (int i = 0; i < joysticks.Count; i++)
            {
                HandleJoyStickConnected(joysticks[i]);
            }
        }

        private string GenerateGamepadId(SDL_JoystickID joystickIndex)
        {
            SDL_GUID guid = SDL_GetJoystickGUIDForID(joystickIndex);

            // We can't compare SDL_GUID directly in SDL3-CS right now.
            ReadOnlySpan<byte> span = guid.data;
            UInt128 guidNum = MemoryMarshal.Read<UInt128>(span);

            // Add a unique identifier to the start of the GUID in case of duplicates.

            if (guidNum == 0)
            {
                return null;
            }

            string id;

            lock (_lock)
            {
                int guidIndex = 0;
                id = guidIndex + "-" + guid;

                while (_gamepadsIds.Contains(id))
                {
                    id = (++guidIndex) + "-" + guid;
                }
            }

            return id;
        }

        private int GetJoystickIndexByGamepadId(string id)
        {
            lock (_lock)
            {
                return _gamepadsIds.IndexOf(id);
            }
        }

        private void HandleJoyStickDisconnected(SDL_JoystickID joystickInstanceId)
        {
            if (_gamepadsInstanceIdsMapping.TryGetValue(joystickInstanceId, out string id))
            {
                _gamepadsInstanceIdsMapping.Remove(joystickInstanceId);

                lock (_lock)
                {
                    _gamepadsIds.Remove(id);
                }

                OnGamepadDisconnected?.Invoke(id);
            }
        }

        private void HandleJoyStickConnected(SDL_JoystickID joystickDeviceId)
        {
            if (SDL_IsGamepad(joystickDeviceId) == SDL_bool.SDL_TRUE)
            {
                if (_gamepadsInstanceIdsMapping.ContainsKey(joystickDeviceId))
                {
                    // Sometimes a JoyStick connected event fires after the app starts even though it was connected before
                    // so it is rejected to avoid doubling the entries.
                    return;
                }

                string id = GenerateGamepadId(joystickDeviceId);

                if (id == null)
                {
                    return;
                }

                if (_gamepadsInstanceIdsMapping.TryAdd(joystickDeviceId, id))
                {
                    lock (_lock)
                    {
                        _gamepadsIds.Add(id);
                    }

                    OnGamepadConnected?.Invoke(id);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SDL2Driver.Instance.OnJoyStickConnected -= HandleJoyStickConnected;
                SDL2Driver.Instance.OnJoystickDisconnected -= HandleJoyStickDisconnected;

                // Simulate a full disconnect when disposing
                foreach (string id in _gamepadsIds)
                {
                    OnGamepadDisconnected?.Invoke(id);
                }

                lock (_lock)
                {
                    _gamepadsIds.Clear();
                }

                SDL2Driver.Instance.Dispose();
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        public unsafe IGamepad GetGamepad(string id)
        {
            var joystickId = _gamepadsInstanceIdsMapping.FirstOrDefault(x => x.Value == id).Key;

            SDL_Gamepad* gamepadHandle = SDL_OpenGamepad(joystickId);

            if ((IntPtr)gamepadHandle == IntPtr.Zero)
            {
                return null;
            }

            return new SDL2Gamepad(gamepadHandle, id);
        }
    }
}
