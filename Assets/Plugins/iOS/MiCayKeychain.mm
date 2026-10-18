#import <Foundation/Foundation.h>
#import <Security/Security.h>

static NSString* const MiCayService = @"vn.tiemmycay.backup";

static NSMutableDictionary* MiCayQuery(NSString* account)
{
    NSMutableDictionary* q = [NSMutableDictionary dictionary];
    q[(__bridge id)kSecClass] = (__bridge id)kSecClassGenericPassword;
    q[(__bridge id)kSecAttrService] = MiCayService;
    q[(__bridge id)kSecAttrAccount] = account;
    return q;
}

extern "C"
{
    bool MiCayKeychainSet(const char* key, const char* value)
    {
        if (key == NULL || value == NULL) return false;
        NSString* account = [NSString stringWithUTF8String:key];
        NSData* data = [[NSString stringWithUTF8String:value] dataUsingEncoding:NSUTF8StringEncoding];
        NSMutableDictionary* query = MiCayQuery(account);
        NSDictionary* update = @{ (__bridge id)kSecValueData: data,
                                  (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleAfterFirstUnlock };
        OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
        if (status == errSecItemNotFound)
        {
            [query addEntriesFromDictionary:update];
            status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
        }
        return status == errSecSuccess;
    }

    char* MiCayKeychainGet(const char* key)
    {
        if (key == NULL) return NULL;
        NSMutableDictionary* query = MiCayQuery([NSString stringWithUTF8String:key]);
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = NULL;
        if (SecItemCopyMatching((__bridge CFDictionaryRef)query, &result) != errSecSuccess || result == NULL) return NULL;
        NSData* data = (__bridge_transfer NSData*)result;
        NSString* text = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        if (text == nil) return NULL;
        const char* utf8 = [text UTF8String];
        char* copy = (char*)malloc(strlen(utf8) + 1);
        strcpy(copy, utf8);
        return copy;
    }

    void MiCayKeychainDelete(const char* key)
    {
        if (key == NULL) return;
        SecItemDelete((__bridge CFDictionaryRef)MiCayQuery([NSString stringWithUTF8String:key]));
    }
}
