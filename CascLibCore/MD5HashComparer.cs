namespace CascLibCore;

internal class MD5HashComparer : IEqualityComparer<MD5Hash> {
    private const uint FnvPrime32 = 16777619;
    private const uint FnvOffset32 = 2166136261;

    public unsafe bool Equals(MD5Hash x, MD5Hash y) {
        for (var i = 0; i < 16; ++i) {
            if (x.Value[i] != y.Value[i]) {
                return false;
            }
        }

        return true;
    }

    public int GetHashCode(MD5Hash obj) {
        return To32BitFnv1aHash(obj);
    }

    private unsafe int To32BitFnv1aHash(MD5Hash toHash) {
        var hash = FnvOffset32;

        var ptr = (uint*)&toHash;

        for (var i = 0; i < 4; i++) {
            hash ^= ptr[i];
            hash *= FnvPrime32;
        }

        return unchecked((int)hash);
    }
}
