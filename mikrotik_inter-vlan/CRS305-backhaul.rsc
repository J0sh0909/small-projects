# 2025-09-13 22:14:30 by RouterOS 7.19.6
#
# model = CRS305-1G-4S+
/interface bridge
add admin-mac=D0:EA:11:29:04:09 auto-mac=no comment=defconf name=bridgeLocal \
    vlan-filtering=yes
/interface ethernet
set [ find default-name=sfp-sfpplus2 ] disabled=yes
set [ find default-name=sfp-sfpplus3 ] disabled=yes
set [ find default-name=sfp-sfpplus4 ] disabled=yes
/interface vlan
add interface=bridgeLocal name=vlan254 vlan-id=254
/interface bridge port
add bridge=bridgeLocal comment=defconf interface=ether1 pvid=777
add bridge=bridgeLocal comment=defconf interface=sfp-sfpplus1 pvid=777
add bridge=bridgeLocal comment=defconf frame-types=\
    admit-only-untagged-and-priority-tagged interface=sfp-sfpplus2 pvid=666
add bridge=bridgeLocal comment=defconf frame-types=\
    admit-only-untagged-and-priority-tagged interface=sfp-sfpplus3 pvid=666
add bridge=bridgeLocal comment=defconf frame-types=\
    admit-only-untagged-and-priority-tagged interface=sfp-sfpplus4 pvid=666
/interface bridge vlan
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=20
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=30
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=40
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=50
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=51
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=52
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=53
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=54
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=55
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=56
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=57
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=58
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=59
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=60
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=70
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=80
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=90
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=100
add bridge=bridgeLocal tagged=bridgeLocal,ether1,sfp-sfpplus1 vlan-ids=254
add bridge=bridgeLocal tagged=ether1,sfp-sfpplus1 vlan-ids=1000
add bridge=bridgeLocal vlan-ids=666
/ip address
add address=192.168.254.2/28 interface=vlan254 network=192.168.254.0
/ipv6 address
add address=fd5d:c575:3bea:254::2 advertise=no interface=vlan254
/system identity
set name=CRS305
