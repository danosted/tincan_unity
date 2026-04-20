using Unity.Netcode;

namespace TinCan.Features.Possession
{
    public static class PossessionRequest
    {
        public class Request
        {
            public IPossessable CurrentPossession { get; set; }
            public IPossessable Target { get; set; }
        }

        public class Response
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }
    }
}
