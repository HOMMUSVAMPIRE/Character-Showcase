using System;
using System.IO;
using System.Text;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A character's input frames (see CharacterInputFrame), one a physics step, kept in a ring of a fixed size: when it's
    /// full, each new frame replaces the oldest, so it holds the latest Capacity steps without allocating. Read oldest
    /// first (this[0]). Written to and read from a small binary file (Save, Load): a header (its format, the step length
    /// it was recorded at, and the tuning's fingerprint, MMotionMotor.TuningFingerprint), then the frames. Replaying
    /// the frames in order to the same character, in the same world, with the same tuning moves it the same way.
    /// </summary>
    public sealed class InputRecording
    {
        /// <summary>What a recording file's name ends with.</summary>
        public const string FileExtension = ".csinput";

        // A file starts with these four bytes, then its format version.
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("CSIR");
        private const int FormatVersion = 1;

        private readonly CharacterInputFrame[] ring;
        // Where the oldest frame is in the ring, and how many it holds.
        private int first, count;

        /// <summary>Starts an empty recording that keeps the latest capacity frames.</summary>
        /// <param name="stepLength">The physics step's length (s) the frames are for.</param>
        /// <param name="tuning">The character's tuning (MMotionMotor.TuningFingerprint), or 0 if not known.</param>
        public InputRecording(int capacity, float stepLength, ulong tuning = 0)
        {
            if (capacity <= 0) { throw new ArgumentOutOfRangeException(nameof(capacity), "A recording holds at least one frame."); }
            ring = NewRing<CharacterInputFrame>(capacity);
            StepLength = stepLength;
            Tuning = tuning;
        }

        /// <summary>The most frames it keeps.</summary>
        public int Capacity => ring.Length;
        /// <summary>How many frames it holds.</summary>
        public int Count => count;
        /// <summary>The physics step's length (s) the frames were recorded at.</summary>
        public float StepLength { get; }
        /// <summary>The fingerprint of the tuning the frames were recorded with (MMotionMotor.TuningFingerprint); 0 if not known.</summary>
        public ulong Tuning { get; }

        /// <summary>A frame, oldest first (0 is the oldest held).</summary>
        public CharacterInputFrame this[int index]
        {
            get
            {
                if ((uint)index >= (uint)count) { throw new ArgumentOutOfRangeException(nameof(index)); }
                return ring[(first + index) % ring.Length];
            }
        }

        /// <summary>Adds the latest frame, replacing the oldest if it's full. Allocates nothing.</summary>
        public void Add(in CharacterInputFrame frame)
        {
            if (count < ring.Length)
            {
                ring[(first + count) % ring.Length] = frame;
                count++;
                return;
            }
            ring[first] = frame;
            first = (first + 1) % ring.Length;
        }

        /// <summary>Empties it.</summary>
        public void Clear()
        {
            first = 0;
            count = 0;
        }

        /// <summary>Writes it to a file (see the class summary), making its folder if need be.</summary>
        public void Save(string path)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) { Directory.CreateDirectory(folder); }
            using FileStream file = File.Create(path);
            Write(file);
        }

        /// <summary>Reads a recording written by Save; it holds exactly the frames written.</summary>
        public static InputRecording Load(string path)
        {
            using FileStream file = File.OpenRead(path);
            return Read(file);
        }

        /// <summary>Writes the header and the frames, oldest first. Each field little-endian, as BinaryWriter writes them.</summary>
        public void Write(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(StepLength);
            writer.Write(Tuning);
            writer.Write(count);
            for (int i = 0; i < count; i++)
            {
                CharacterInputFrame frame = this[i];
                writer.Write(frame.tick);
                writer.Write(frame.move.x);
                writer.Write(frame.move.y);
                writer.Write(frame.lookYaw);
                writer.Write(frame.lookPitch);
                writer.Write((uint)frame.held);
            }
        }

        /// <summary>Reads what Write wrote. Throws InvalidDataException if it isn't a recording, or is of a newer format.</summary>
        public static InputRecording Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length || Encoding.ASCII.GetString(magic) != "CSIR")
            {
                throw new InvalidDataException("Not an input recording.");
            }
            int version = reader.ReadInt32();
            if (version != FormatVersion) { throw new InvalidDataException($"An input recording of format {version}; this reads {FormatVersion}."); }
            float stepLength = reader.ReadSingle();
            ulong tuning = reader.ReadUInt64();
            int frames = reader.ReadInt32();
            if (frames < 0) { throw new InvalidDataException("An input recording with a negative frame count."); }
            var recording = new InputRecording(Math.Max(frames, 1), stepLength, tuning);
            for (int i = 0; i < frames; i++)
            {
                var frame = new CharacterInputFrame { tick = reader.ReadUInt32() };
                frame.move.x = reader.ReadSingle();
                frame.move.y = reader.ReadSingle();
                frame.lookYaw = reader.ReadSingle();
                frame.lookPitch = reader.ReadSingle();
                frame.held = (InputButtons)reader.ReadUInt32();
                recording.Add(frame);
            }
            return recording;
        }

        // The frame must stay unmanaged (blittable: see CharacterInputFrame), for rings, files and sending as it is: the
        // ring is made through this, which fails to compile if a field that isn't is added to it.
        private static T[] NewRing<T>(int capacity) where T : unmanaged => new T[capacity];
    }
}
