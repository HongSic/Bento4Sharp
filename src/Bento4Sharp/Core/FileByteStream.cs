// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4FileByteStream.h, Source/C++/System/StdC/Ap4StdCFileByteStream.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

using System;
using System.IO;

namespace Bento4
{

    /*----------------------------------------------------------------------
    |   AP4_FileByteStream
    +---------------------------------------------------------------------*/
    // PORT NOTE: FILE* is replaced by System.IO.Stream. FileByteStream is
    // IDisposable (Dispose closes the file; the standard streams are never
    // closed). FileByteStream.Create returns a FileByteStream that wraps the
    // StdcFileByteStream (upstream returns the AP4_StdcFileByteStream itself),
    // so that `stream as FileByteStream` / `using` work on the result.
    public class FileByteStream : ByteStream, IDisposable
    {
        // types
        public enum Mode {
            STREAM_MODE_READ        = 0,
            STREAM_MODE_WRITE       = 1,
            STREAM_MODE_READ_WRITE  = 2
        }

        /*----------------------------------------------------------------------
        |   AP4_FileByteStream::Create
        +---------------------------------------------------------------------*/
        /// <summary>
        /// Create a stream from a file (opened or created).
        /// </summary>
        /// <param name="name">Name of the file to open or create</param>
        /// <param name="mode">Mode to use for the file</param>
        /// <param name="stream">Reference to a pointer where the stream object will
        /// be returned</param>
        /// <returns><see cref="Ap4Result.SUCCESS"/> if the file can be opened or created, or an error code if
        /// it cannot</returns>
        // PORT NOTE: the names "-stdin", "-stdout", "-stderr" select the standard
        // streams; the stream object returned is a FileByteStream.
        public static Ap4Result Create(string name, Mode mode, out ByteStream stream)
        {
            stream = null;
            ByteStream stdc_stream;
            Ap4Result result = StdcFileByteStream.Create(null, name, mode, out stdc_stream);
            if (result.Failed()) return result;
            stream = new FileByteStream(stdc_stream);
            return Ap4Result.SUCCESS;
        }

        // constructors
        public FileByteStream(ByteStream @delegate)
        {
            _delegate = @delegate;
        }

        /*----------------------------------------------------------------------
        |   AP4_FileByteStream::AP4_FileByteStream
        +---------------------------------------------------------------------*/
        /// <remarks>Deprecated.</remarks>
        // PORT NOTE: throws Ap4Exception if the file cannot be opened.
        public FileByteStream(string name, Mode mode)
        {
            ByteStream stream = null;
            Ap4Result result = StdcFileByteStream.Create(this, name, mode, out stream);
            if (result.Failed()) throw new Ap4Exception(result);

            _delegate = stream;
        }

        // AP4_ByteStream methods
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytesToRead,
                                              out uint bytesRead) {
            return _delegate.ReadPartial(buffer, offset, bytesToRead, out bytesRead);
        }
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytesToWrite,
                                               out uint bytesWritten) {
            return _delegate.WritePartial(buffer, offset, bytesToWrite, out bytesWritten);
        }
        public override Ap4Result Seek(ulong position)      { return _delegate.Seek(position); }
        public override Ap4Result Tell(out ulong position)  { return _delegate.Tell(out position); }
        public override Ap4Result GetSize(out ulong size)   { return _delegate.GetSize(out size);  }
        public override Ap4Result Flush()                   { return _delegate.Flush();        }

        /// <summary>Flushes and closes the underlying file (replaces the final Release() of upstream).</summary>
        public void Dispose()
        {
            if (_delegate is IDisposable) ((IDisposable)_delegate).Dispose();
            GC.SuppressFinalize(this);
        }

        // members
        protected ByteStream _delegate;
    }

    /*----------------------------------------------------------------------
    |   AP4_StdcFileByteStream
    +---------------------------------------------------------------------*/
    public class StdcFileByteStream : ByteStream, IDisposable
    {
        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::Create
        +---------------------------------------------------------------------*/
        public static Ap4Result Create(FileByteStream      delegator,
                                       string              name,
                                       FileByteStream.Mode mode,
                                       out ByteStream      stream)
        {
            // default value
            stream = null;

            // check arguments
            if (name == null) return Ap4Result.ERROR_INVALID_PARAMETERS;

            // open the file
            Stream  file = null;
            bool    is_std = false;
            ulong   size = 0;
            if (name == "-stdin" || name == "-stdin#") {
                // PORT NOTE: the '#' suffix switches the stream to binary mode on
                // Windows upstream; .NET standard streams are always binary.
                file = Console.OpenStandardInput();
                is_std = true;
            } else if (name == "-stdout" || name == "-stdout#") {
                file = Console.OpenStandardOutput();
                is_std = true;
            } else if (name == "-stderr") {
                file = Console.OpenStandardError();
                is_std = true;
            } else {
                try {
                    switch (mode) {
                      case FileByteStream.Mode.STREAM_MODE_READ:
                        // "rb"
                        file = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        break;

                      case FileByteStream.Mode.STREAM_MODE_WRITE:
                        // "wb+"
                        file = new FileStream(name, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                        break;

                      case FileByteStream.Mode.STREAM_MODE_READ_WRITE:
                        // "r+b"
                        file = new FileStream(name, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                        break;

                      default:
                        return Ap4Result.ERROR_INVALID_PARAMETERS;
                    }
                } catch (FileNotFoundException) {
                    return Ap4Result.ERROR_NO_SUCH_FILE;            // ENOENT
                } catch (DirectoryNotFoundException) {
                    return Ap4Result.ERROR_NO_SUCH_FILE;            // ENOENT
                } catch (ArgumentException) {
                    if (name.Length == 0) {
                        return Ap4Result.ERROR_NO_SUCH_FILE;            // ENOENT (fopen("") upstream)
                    } else {
                        return Ap4Result.ERROR_CANNOT_OPEN_FILE;
                    }
                } catch (UnauthorizedAccessException) {
                    if (mode != FileByteStream.Mode.STREAM_MODE_READ && Directory.Exists(name)) {
                        return Ap4Result.ERROR_CANNOT_OPEN_FILE;        // EISDIR (a directory cannot be opened for writing)
                    } else {
                        return Ap4Result.ERROR_PERMISSION_DENIED;       // EACCES
                    }
                } catch (System.Security.SecurityException) {
                    return Ap4Result.ERROR_PERMISSION_DENIED;       // EACCES
                } catch (Exception) {
                    return Ap4Result.ERROR_CANNOT_OPEN_FILE;
                }

                // get the size
                try {
                    if (file.CanSeek) {
                        size = (ulong)file.Length;
                    }
                } catch (Exception) {
                    size = 0;
                }
            }

            stream = new StdcFileByteStream(delegator, file, size, is_std);
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::AP4_StdcFileByteStream
        +---------------------------------------------------------------------*/
        public StdcFileByteStream(FileByteStream  delegator,
                                  Stream          file,
                                  ulong           size) : this(delegator, file, size, false)
        {
        }

        private StdcFileByteStream(FileByteStream  delegator,
                                   Stream          file,
                                   ulong           size,
                                   bool            is_std)
        {
            _delegator = delegator;
            _file = file;
            _fileIsStd = is_std;
            _position = 0;
            _size = size;
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::~AP4_StdcFileByteStream
        +---------------------------------------------------------------------*/
        public void Dispose()
        {
            Stream file = _file;
            if (file != null) {
                _file = null;
                try {
                    if (_fileIsStd) {
                        // stdin, stdout and stderr are not closed
                        if (file.CanWrite) file.Flush();
                    } else {
                        file.Dispose();
                    }
                } catch (Exception) {
                    // fclose() errors are ignored upstream
                }
            }
            GC.SuppressFinalize(this);
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::ReadPartial
        +---------------------------------------------------------------------*/
        public override Ap4Result ReadPartial(byte[]   buffer,
                                              int      offset,
                                              uint     bytesToRead,
                                              out uint bytesRead)
        {
            int nbRead = 0;
            bool failed = false;

            if (_file == null) {
                bytesRead = 0;
                return Ap4Result.ERROR_READ_FAILED;
            }

            // PORT NOTE: fread() only returns a short count at the end of the file
            // (or on error); Stream.Read may return early (pipes), so loop.
            try {
                while ((uint)nbRead < bytesToRead) {
                    int chunk = _file.Read(buffer, offset+nbRead, (int)Math.Min(bytesToRead-(uint)nbRead, (uint)int.MaxValue));
                    if (chunk <= 0) {
                        _eof = true;
                        break;
                    }
                    nbRead += chunk;
                }
            } catch (Exception) {
                failed = true;
            }

            if (nbRead > 0) {
                bytesRead = (uint)nbRead;
                _position += (ulong)nbRead;
                return Ap4Result.SUCCESS;
            } else if (_eof && !failed) {
                // feof(m_File)
                bytesRead = 0;
                return Ap4Result.ERROR_EOS;
            } else {
                bytesRead = 0;
                return Ap4Result.ERROR_READ_FAILED;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::WritePartial
        +---------------------------------------------------------------------*/
        public override Ap4Result WritePartial(byte[]   buffer,
                                               int      offset,
                                               uint     bytesToWrite,
                                               out uint bytesWritten)
        {
            bytesWritten = 0;

            if (bytesToWrite == 0) return Ap4Result.SUCCESS;
            if (_file == null) return Ap4Result.ERROR_WRITE_FAILED;

            int nbWritten;
            try {
                nbWritten = (int)Math.Min(bytesToWrite, (uint)int.MaxValue);
                _file.Write(buffer, offset, nbWritten);
            } catch (Exception) {
                nbWritten = 0;
            }

            if (nbWritten > 0) {
                bytesWritten = (uint)nbWritten;
                _position += (ulong)nbWritten;
                if (_position > _size) {
                    _size = _position;
                }
                return Ap4Result.SUCCESS;
            } else {
                bytesWritten = 0;
                return Ap4Result.ERROR_WRITE_FAILED;
            }
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::Seek
        +---------------------------------------------------------------------*/
        public override Ap4Result Seek(ulong position)
        {
            // shortcut
            if (position == _position) return Ap4Result.SUCCESS;

            if (_file == null || !_file.CanSeek || position > (ulong)long.MaxValue) {
                return Ap4Result.FAILURE;
            }
            try {
                _file.Seek((long)position, SeekOrigin.Begin);
            } catch (Exception) {
                return Ap4Result.FAILURE;
            }
            _eof = false;
            _position = position;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::Tell
        +---------------------------------------------------------------------*/
        public override Ap4Result Tell(out ulong position)
        {
            position = _position;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::GetSize
        +---------------------------------------------------------------------*/
        public override Ap4Result GetSize(out ulong size)
        {
            size = _size;
            return Ap4Result.SUCCESS;
        }

        /*----------------------------------------------------------------------
        |   AP4_StdcFileByteStream::Flush
        +---------------------------------------------------------------------*/
        public override Ap4Result Flush()
        {
            // PORT NOTE: upstream returns AP4_FAILURE only when fflush() returns
            // a value > 0, which never happens (0 or EOF): always AP4_SUCCESS.
            try {
                if (_file != null && _file.CanWrite) _file.Flush();
            } catch (Exception) {
            }
            return Ap4Result.SUCCESS;
        }

        // members
        private ByteStream  _delegator;
        private Stream      _file;
        private bool        _fileIsStd;
        private bool        _eof;
        private ulong       _position;
        private ulong       _size;
    }
}
