// Bento4Sharp - pure C# implementation of Bento4 (https://github.com/axiomatic-systems/Bento4)
// Ported from: Source/C++/Core/Ap4Results.h, Ap4Results.cpp
// Original work Copyright 2002-2008 Axiomatic Systems, LLC
// Licensed under the GNU General Public License v2 or later. See LICENSE.

namespace Bento4
{

    /// <summary>AP4_Result codes (AP4_SUCCESS, AP4_FAILURE, AP4_ERROR_xxx).</summary>
    public enum Ap4Result : int
    {
        SUCCESS                             =  0,
        FAILURE                             = -1,
        ERROR_OUT_OF_MEMORY                 = -2,
        ERROR_INVALID_PARAMETERS            = -3,
        ERROR_NO_SUCH_FILE                  = -4,
        ERROR_PERMISSION_DENIED             = -5,
        ERROR_CANNOT_OPEN_FILE              = -6,
        ERROR_EOS                           = -7,
        ERROR_WRITE_FAILED                  = -8,
        ERROR_READ_FAILED                   = -9,
        ERROR_INVALID_FORMAT                = -10,
        ERROR_NO_SUCH_ITEM                  = -11,
        ERROR_OUT_OF_RANGE                  = -12,
        ERROR_INTERNAL                      = -13,
        ERROR_INVALID_STATE                 = -14,
        ERROR_LIST_EMPTY                    = -15,
        ERROR_LIST_OPERATION_ABORTED        = -16,
        ERROR_INVALID_RTP_CONSTRUCTOR_TYPE  = -17,
        ERROR_NOT_SUPPORTED                 = -18,
        ERROR_INVALID_TRACK_TYPE            = -19,
        ERROR_INVALID_RTP_PACKET_EXTRA_DATA = -20,
        ERROR_BUFFER_TOO_SMALL              = -21,
        ERROR_NOT_ENOUGH_DATA               = -22
    }

    /*----------------------------------------------------------------------
    |   macros
    +---------------------------------------------------------------------*/
    public static class Ap4ResultExtensions
    {
        /// <summary>AP4_FAILED(result)</summary>
        public static bool Failed(this Ap4Result result) { return result != Ap4Result.SUCCESS; }

        /// <summary>AP4_SUCCEEDED(result)</summary>
        public static bool Succeeded(this Ap4Result result) { return result == Ap4Result.SUCCESS; }
    }

    // PORT NOTE: AP4_CHECK(x) has no macro equivalent; write
    //   { Ap4Result _result = x; if (_result.Failed()) return _result; }

    public static partial class Ap4
    {
        /*----------------------------------------------------------------------
        |   AP4_ResultText
        +---------------------------------------------------------------------*/
        public static string ResultText(Ap4Result result)
        {
            return ResultText((int)result);
        }

        public static string ResultText(int result)
        {
            switch ((Ap4Result)result) {
                case Ap4Result.SUCCESS:                             return "AP4_SUCCESS";
                case Ap4Result.FAILURE:                             return "AP4_FAILURE";
                case Ap4Result.ERROR_OUT_OF_MEMORY:                 return "AP4_ERROR_OUT_OF_MEMORY";
                case Ap4Result.ERROR_INVALID_PARAMETERS:            return "AP4_ERROR_INVALID_PARAMETERS";
                case Ap4Result.ERROR_NO_SUCH_FILE:                  return "AP4_ERROR_NO_SUCH_FILE";
                case Ap4Result.ERROR_PERMISSION_DENIED:             return "AP4_ERROR_PERMISSION_DENIED";
                case Ap4Result.ERROR_CANNOT_OPEN_FILE:              return "AP4_ERROR_CANNOT_OPEN_FILE";
                case Ap4Result.ERROR_EOS:                           return "AP4_ERROR_EOS";
                case Ap4Result.ERROR_WRITE_FAILED:                  return "AP4_ERROR_WRITE_FAILED";
                case Ap4Result.ERROR_READ_FAILED:                   return "AP4_ERROR_READ_FAILED";
                case Ap4Result.ERROR_INVALID_FORMAT:                return "AP4_ERROR_INVALID_FORMAT";
                case Ap4Result.ERROR_NO_SUCH_ITEM:                  return "AP4_ERROR_NO_SUCH_ITEM";
                case Ap4Result.ERROR_OUT_OF_RANGE:                  return "AP4_ERROR_OUT_OF_RANGE";
                case Ap4Result.ERROR_INTERNAL:                      return "AP4_ERROR_INTERNAL";
                case Ap4Result.ERROR_INVALID_STATE:                 return "AP4_ERROR_INVALID_STATE";
                case Ap4Result.ERROR_LIST_EMPTY:                    return "AP4_ERROR_LIST_EMPTY";
                case Ap4Result.ERROR_LIST_OPERATION_ABORTED:        return "AP4_ERROR_LIST_OPERATION_ABORTED";
                case Ap4Result.ERROR_INVALID_RTP_CONSTRUCTOR_TYPE:  return "AP4_ERROR_INVALID_RTP_CONSTRUCTOR_TYPE";
                case Ap4Result.ERROR_NOT_SUPPORTED:                 return "AP4_ERROR_NOT_SUPPORTED";
                case Ap4Result.ERROR_INVALID_TRACK_TYPE:            return "AP4_ERROR_INVALID_TRACK_TYPE";
                case Ap4Result.ERROR_INVALID_RTP_PACKET_EXTRA_DATA: return "AP4_ERROR_INVALID_RTP_PACKET_EXTRA_DATA";
                case Ap4Result.ERROR_BUFFER_TOO_SMALL:              return "AP4_ERROR_BUFFER_TOO_SMALL";
                case Ap4Result.ERROR_NOT_ENOUGH_DATA:               return "AP4_ERROR_NOT_ENOUGH_DATA";
                default:                                            return "UNKNOWN";
            }
        }
    }
}
