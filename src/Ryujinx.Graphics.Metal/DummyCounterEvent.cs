using Ryujinx.Graphics.GAL;

namespace Ryujinx.Graphics.Metal
{
    class DummyCounterEvent : ICounterEvent
    {
        public DummyCounterEvent()
        {
            Invalid = false;
        }

        public bool Invalid { get; set; }
        public bool ReserveForHostAccess()
        {
            return true;
        }

        public void Flush() { }

        public void Dispose() { }

    }
}
