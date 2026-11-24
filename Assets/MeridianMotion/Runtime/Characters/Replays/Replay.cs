using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MeridianMotion.Characters
{
    /// <summary>
    /// A recorded run of the local player's character (ReplayRecorder): the input frames it was given (an InputRecording),
    /// and keyframes, each the character's and the level's moving parts' state (StateSerializer) and the level's tick at a
    /// frame, the first at the run's start. Played back (ReplayRecorder.Play), the character's put back as at the first
    /// keyframe and given the frames; snapping to each keyframe as it's reached keeps a replay on its path when it's
    /// played on another build than recorded it (the movement is deterministic on one build only). Written to a file
    /// (Save, Load): its header (the format, the scene, when it was made, the build's version), its keyframes, its frames.
    /// </summary>
    public sealed class Replay
    {
        /// <summary>What a replay file's name ends with.</summary>
        public const string FileExtension = ".mmreplay";

        // The file's first bytes, and its format (raised when what's written changes).
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MMREPLAY");
        private const int Format = 1;

        /// <summary>One keyframe: where the run was at a frame.</summary>
        public sealed class Keyframe
        {
            /// <summary>The frame (its index in Frames) the state's from: the state just before that frame's step.</summary>
            public int Frame;
            /// <summary>The level's tick at that step (LevelClock.Tick).</summary>
            public long LevelTick;
            /// <summary>The character's state (StateSerializer.WriteCharacter).</summary>
            public byte[] Character;
            /// <summary>The level's moving parts' state (StateSerializer.WriteWorld).</summary>
            public byte[] World;
        }

        /// <summary>The scene it was recorded in (its name).</summary>
        public string Scene;
        /// <summary>When it was recorded (UTC).</summary>
        public DateTime Recorded;
        /// <summary>The build that recorded it (Application.version, and the editor's or player's Unity version).</summary>
        public string Build;
        /// <summary>The keyframes, in frame order; the first is the run's start (frame 0).</summary>
        public readonly List<Keyframe> Keyframes = new();
        /// <summary>The input frames, one a step.</summary>
        public InputRecording Frames;

        /// <summary>How long it runs, in seconds.</summary>
        public float Seconds => Frames != null ? Frames.Count * Frames.StepLength : 0f;

        /// <summary>The folder replays are saved in: Replays, in the player's data folder (Application.persistentDataPath).</summary>
        public static string Folder => Path.Combine(Application.persistentDataPath, "Replays");

        /// <summary>This build, as a replay records it.</summary>
        public static string ThisBuild => $"{Application.version} (Unity {Application.unityVersion}{(Application.isEditor ? ", editor" : "")})";

        /// <summary>A new file's path in Folder for a replay of the scene, named for the scene and the time.</summary>
        public static string NewPath(string scene) => Path.Combine(Folder, $"{scene} {DateTime.Now:yyyy-MM-dd HH-mm-ss}{FileExtension}");

        /// <summary>The newest replay file of the scene in the folder (Folder, if none's given), or null.</summary>
        public static string LatestFor(string scene, string folder = null)
        {
            folder ??= Folder;
            if (!Directory.Exists(folder)) { return null; }
            string latest = null;
            DateTime newest = DateTime.MinValue;
            foreach (string path in Directory.GetFiles(folder, $"{scene} *{FileExtension}"))
            {
                DateTime written = File.GetLastWriteTimeUtc(path);
                if (written > newest || (written == newest && string.CompareOrdinal(path, latest) > 0))
                {
                    newest = written;
                    latest = path;
                }
            }
            return latest;
        }

        /// <summary>Writes it to a file, making its folder if need be.</summary>
        public void Save(string path)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) { Directory.CreateDirectory(folder); }
            using FileStream stream = File.Create(path);
            Write(stream);
        }

        /// <summary>Reads a replay file. Throws InvalidDataException if it isn't one, or is of a newer format.</summary>
        public static Replay Load(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Read(stream);
        }

        /// <summary>Writes the header, the keyframes and the frames.</summary>
        public void Write(Stream stream)
        {
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write(Magic);
            writer.Write(Format);
            writer.Write(Scene ?? "");
            writer.Write(Recorded.ToBinary());
            writer.Write(Build ?? "");
            writer.Write(Keyframes.Count);
            foreach (Keyframe keyframe in Keyframes)
            {
                writer.Write(keyframe.Frame);
                writer.Write(keyframe.LevelTick);
                WriteBytes(writer, keyframe.Character);
                WriteBytes(writer, keyframe.World);
            }
            writer.Flush();
            (Frames ?? new InputRecording(1, Time.fixedDeltaTime)).Write(stream);
        }

        /// <summary>Reads what Write wrote.</summary>
        public static Replay Read(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            byte[] magic = reader.ReadBytes(Magic.Length);
            if (magic.Length != Magic.Length || !System.Linq.Enumerable.SequenceEqual(magic, Magic)) { throw new InvalidDataException("This isn't a replay file."); }
            int format = reader.ReadInt32();
            if (format > Format) { throw new InvalidDataException($"This replay is of a newer format ({format}) than this build reads ({Format})."); }
            var replay = new Replay
            {
                Scene = reader.ReadString(),
                Recorded = DateTime.FromBinary(reader.ReadInt64()),
                Build = reader.ReadString(),
            };
            int keyframes = reader.ReadInt32();
            for (int i = 0; i < keyframes; i++)
            {
                replay.Keyframes.Add(new Keyframe
                {
                    Frame = reader.ReadInt32(),
                    LevelTick = reader.ReadInt64(),
                    Character = ReadBytes(reader),
                    World = ReadBytes(reader),
                });
            }
            replay.Frames = InputRecording.Read(stream);
            return replay;
        }

        private static void WriteBytes(BinaryWriter writer, byte[] bytes)
        {
            writer.Write(bytes?.Length ?? 0);
            if (bytes != null) { writer.Write(bytes); }
        }

        private static byte[] ReadBytes(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0) { throw new InvalidDataException("A replay's keyframe is damaged."); }
            return reader.ReadBytes(length);
        }
    }
}
