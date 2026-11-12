// Silent Contract: Apple's App Tracking Transparency question (iOS 14+), called from Privacy.cs.
#import <Foundation/Foundation.h>
#import <AppTrackingTransparency/AppTrackingTransparency.h>

static int sc_att_answered = 0;     // the completion handler ran (an ignored request also lands here, still undetermined)

extern "C" {
    int SC_AttAnswered() { return sc_att_answered; }

    // 0 not determined, 1 restricted, 2 denied, 3 authorized (iOS < 14: 3, no question exists)
    int SC_AttStatus()
    {
        if (@available(iOS 14, *)) return (int)[ATTrackingManager trackingAuthorizationStatus];
        return 3;
    }

    void SC_AttRequest()
    {
        sc_att_answered = 0;
        if (@available(iOS 14, *))
        {
            dispatch_async(dispatch_get_main_queue(), ^{
                [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) { sc_att_answered = 1; }];
            });
        }
    }
}
