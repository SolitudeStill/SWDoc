namespace BalaDoc.Exception
{
    [System.Serializable]
    public class InitializeNonemptyDirectoryException : System.Exception
    {
        public InitializeNonemptyDirectoryException() { }
        public InitializeNonemptyDirectoryException(string message) : base(message) { }
        public InitializeNonemptyDirectoryException(string message, System.Exception inner) : base(message, inner) { }
        protected InitializeNonemptyDirectoryException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
    [System.Serializable]
    public class ComposerVersionMismatchException : System.Exception
    {
        public ComposerVersionMismatchException() { }
        public ComposerVersionMismatchException(string message) : base(message) { }
        public ComposerVersionMismatchException(string message, System.Exception inner) : base(message, inner) { }
        protected ComposerVersionMismatchException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}