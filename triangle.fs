#ifdef GL_ES
precision mediump float;
#endif

const int MAX_MARCHING_STEPS = 255;
const float MIN_DIST = 0.0;
const float MAX_DIST = 150.0;
const float EPSILON = 0.0001;

uniform vec2 u_resolution;
uniform vec2 u_mouse;
uniform float u_time;

const float PI = 3.14159;

struct light {
    vec3 pos;
    vec3 ambientColor;
    vec3 diffuseColor;
    vec3 specularColor;
    float intensity;
} ;

struct rayCast {
    vec3 direction;
    float magnitude;
    object hit;
    vec3 hitPoint;
} ray;

struct primitive {
    vec3 color;
    vec3 pos;
    float sdf;
};

struct object {
  vec3 position;
  vec3 dimensions;
  vec2 radii;
  float size;
  vec3 ambientColor;
  vec3 diffuseColor;
  vec3 specularColor;
  float hit;

  object(vec3 pos,vec3 dim,vec2 r,float s,vec3 aColor,vec3 dColor,vec3 specColor){
    position = pos;
    dimensions = dim;
    radii = r;
    size = s;
    ambientColor = aColor;
    diffuseColor = dColor;
    specularColor = specColor;
    hit = EPSILON;
  }
};

//global background color declaration
vec3 background = vec3(0.8,0.8,0.9);

/*
    Checks if the point is inside or outside of
    a unit sphere centered at the origin
*/

/*
    is the pt inside or outside of any object in the scene
    currently only checking one sphere
*/

float sdSphere( vec3 p, float s )
{
  return length(p)-s;
}


float sdBox( vec3 p, vec3 b )
{
  vec3 d = abs(p) - b;
  return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,vec3(0.0)));
}

float sdTorus( vec3 p, vec2 t )
{
  vec2 q = vec2(length(p.xz)-t.x,p.y);
  return length(q)-t.y;
}

float opTwistTorus( vec3 p, vec2 r )
{
    float c = cos(2.0*p.y);
    float s = sin(2.0*p.y);
    mat2  m = mat2(c,-s,s,c);
    vec3  q = vec3(m*p.xz,p.y);
    return sdTorus(q , r);
}

float smin( float a, float b, float k )
{
    float h = clamp( 0.5+0.5*(b-a)/k, 0.0, 1.0 );
    return mix( b, a, h ) - k*h*(1.0-h);
}

float smoothUnion( float d1, float d2 )
{
    return smin(d1,d2, 0.5);
}

float un(float d1, float d2) {
    return min(d1,d2);
}

float sceneSDF(vec3 pt) {
    object floor = object(pt-vec3(0,-1.7,0),vec3(30,0.0,20.0),vec2(0),0.0,floorCheckerboard(pt),floorCheckerboard(pt),floorCheckerboard(pt));
    object sphere = object(pt-vec3(sin(u_time),-0.5+(sin(u_time)*0.3),cos(u_time)), vec(0),vec2(0),0.5,vec3(0.630,0.0,0.076), vec3(0.730,0.064,0.176), vec3(0.830,0.164,0.276));
    object mirrorSphere = object(pt-vec3(2.811,1.0,-3.0),vec3(0),vec2(0),2.0,vec3(0),vec3(0),vec3(0));
    object mirrorTorus = object(pt-vec3(0,0.5,0.0),vec3(0),vec2(12.0,1.5),0.0,vec3(0),vec3(0),vec3(0));
    object twistTorus = object(pt- vec3(-1.5,0.5,-4.0),vec3(0),vec2(1.0,0.25),0.0,vec3(0.630,0.0,0.076), vec3(0.730,0.064,0.176), vec3(0.830,0.164,0.276));

    floor.hit = sdBox(floor.position, floor.dimensions);
    sphere.hit = sdSphere(sphere.position, sphere.size);
    mirrorSphere.hit = sdSphere(mirrorSphere.position, mirrorSphere.size);
    mirrorTorus.hit = sdTorus(mirrorTorus.position,mirrorTorus.radii);
    twistTorus.hit = opTwistTorus(twistTorus.position,twistTorus.radii);


    float value = un(un(un(floor.hit, sphere.hit),un(mirrorTorus.hit, mirrorSphere.hit)),twistTorus.hit);

    if(value == floor.hit) {
        ray.hit = floor;
    } else if (value == sphere.hit) {
        ray.hit = sphere;
    } else if (value == twistTorus.hit) {
        ray.hit = twistTorus;
    } else if (value == mirrorSphere.hit ) {
        ray.hit = mirrorSphere;
    } else if (value == mirrorTorus.hit ) {
        ray.hit = mirrorTorus;
    }

    return value;// + sdSphere(pt, 1.0);//sdBox(pt, vec3(1.0,-1.0,1.0));// + sphereAtPos(pt, vec3(0.0,2.0,0.0));
}

/*
    key function of the raymarch
    find the shortest distance to the surface of an object in the scene
    eye - the origin of the ray
    direction - the normalized direction to move (direction ray is facing)
    start/end - starting point on the ray, and max distance to move
*/

float shortestDistanceToSurface(vec3 startPoint, vec3 direction, float start, float end) {
    float depth = start;
    float dist;
    for(int i = 0; i < MAX_MARCHING_STEPS; i++) {
        vec3 nextPt = startPoint + depth * direction;
        dist = sceneSDF(nextPt);
        if(dist < EPSILON)
            return depth;
        depth += dist;
        if(depth >= end) {
            return end;
        }
    }
    return end;
}


/*
    ray direction - find the normalized direction to march in from
        the eye to a single pixel on the screen.

    perameters:
    fieldOfView - vertical fov in degrees
    size - resolution of the output image
    fragCoord - the x/y coordinate of the pizel in the output
                (screen x,y not adjusted UV)
*/

vec3 rayDirection(float fieldOfView, vec2 size, vec2 fragCoord) {
    vec2 xy = fragCoord - size/2.0;
    float z = size.y/tan(radians(fieldOfView)/2.0);
    return normalize(vec3(xy,-z));
}

vec3 estimateNormal(vec3 pt) {
    return normalize(vec3(
        sceneSDF(vec3(pt.x+EPSILON, pt.yz)) - sceneSDF(vec3(pt.x-EPSILON,pt.yz)),
        sceneSDF(vec3(pt.x, pt.y+EPSILON, pt.z)) - sceneSDF(vec3(pt.x,pt.y-EPSILON,pt.z)),
        sceneSDF(vec3(pt.xy,pt.z+EPSILON)) - sceneSDF(vec3(pt.xy, pt.z - EPSILON))
    ));
}

//lighting start
vec3 diffuseLighting(vec3 pt, light currentLight, vec3 normal) {
    vec3 lightDir = normalize(currentLight.pos-pt);
    float lDotn = dot(lightDir, normal);
    return (currentLight.diffuseColor * max(lDotn,0.0) * currentLight.intensity);
}

vec3 specularLighting(vec3 pt, vec3 normal, vec3 eye, light currentLight) {
    float shinyness = 8.0;
    vec3 l = normalize(currentLight.pos - pt);
    vec3 r = normalize(reflect(-l, normal));
    vec3 v = normalize(eye - pt);
    float rdotV = max(dot(r,v), 0.0);
    return (currentLight.specularColor * currentLight.intensity * pow(rdotV, shinyness));
}

vec3 floorCheckerboard(vec3 pt) {
    vec3 color = vec3(0.981,0.985,0.995);

    float tile = floor(pt.x-(2.0 *floor(pt.x/2.0))) - floor(pt.z-(2.0*floor(pt.z/2.0)));
    if (tile < 0.0) {
        tile = 1.0;
    }


    return color * (1.0 - tile);
}

bool shadow(vec3 pt, light currentLight) {

    float dist = shortestDistanceToSurface(pt, normalize(currentLight.pos - pt), MIN_DIST+ 0.001, length(currentLight.pos - pt));

    bool hit;
    if (dist < length(currentLight.pos - pt)) {
        hit = true;
    } else if (dist == abs(length(currentLight.pos - pt))) {
        hit = false;
    }

    return  hit;
}

vec3 lighting(vec3 pt, vec3 eye, light currentLight, object currObject) {
    vec3 ambientLight = currentLight.ambientColor;
    vec3 normal = estimateNormal(pt);

    vec3 diffuse = diffuseLighting(pt,currentLight, normal);
    vec3 specular = specularLighting(pt, normal, eye, currentLight);

    bool shadow = shadow(pt, currentLight);

    //vec3 ptColor = (ambientLight + (diffuse + specular * float(!shadow)) + (float(shadow) * vec3(-0.2, -0.8, -0.8))) * objectColor;
    vec3 ptColor = (ambientLight + diffuse + specular) * currObject.specularColor;


    if (shadow) {
        ptColor = (float(shadow) * vec3(-0.8, -0.8, -0.1) + ambientLight) * currObject.specularColor;
    }

    return ptColor;
}

vec3 getObjectColor(vec3 pt) {
    vec3 sphereColor = vec3(0.830,0.164,0.276);
    vec3 floorColor = floorCheckerboard(pt);
    vec3 objectColor;
    if (ray.hit == floor) {
        objectColor = floor.specularColor;
    } else if (ray.hit == sphere) {
        objectColor = sphere.specularColor;
    }

    return objectColor;
}

vec3 mirror(vec3 pt, vec3 eye, light currentLight, object currObject) {
    vec3 objectColor, normal, v, reflectedV;
    float dist, noHit;
    vec3 mirrorColor = vec3(-0.0);

    const int numBounces = 6;
    int bounces = 0;
    for (int i = 0; i < numBounces; i ++) {
        normal = estimateNormal(pt);
        v = normalize(eye - pt);
        reflectedV = normalize(reflect(-v, normal));
        dist = shortestDistanceToSurface(pt, reflectedV, MIN_DIST + 0.0001, MAX_DIST);
        noHit = step(dist, MAX_DIST - EPSILON);

        pt = pt + (normalize(reflectedV) * dist);
        bounces = i;
        if(ray.hit == floor || ray.hit == sphere || ray.hit == twistTorus) {
            objectColor = lighting(pt, reflectedV, currentLight, currObject);
            break;
        }
    }
    if (bounces == numBounces-1) {
        objectColor = lighting(pt, reflectedV, currentLight, vec3(0.6));
    }

    vec3 reflectionColor = ((1.0-noHit) * (background + mirrorColor)) + (noHit * (currObject.specularColor + mirrorColor));

    return lighting(pt, eye, currentLight, reflectionColor);
}

vec3 lightingStyle(vec3 pt, vec3 eye, light currentLight, object currObject) {
    vec3 color;
    vec3 objectColor = currObject.specularColor;


    if (ray.hit == floor || ray.hit == sphere || ray.hit == twistTorus) {
        color = lighting(pt, eye, currentLight, currObject);
    } else if (ray.hit == mirrorTorus || ray.hit == mirrorSphere) {
        color = mirror(pt, eye, currentLight, currObject);
    }

    return color;
}

//lighting end

//just testing git bruh

mat3 setCamera(vec3 eye, vec3 center, float rotation) {
    vec3 forward = normalize(center - eye);
    vec3 orientation = vec3(sin(rotation),cos(rotation), 0.0);
    vec3 left = normalize(cross(forward,orientation));
    vec3 up = normalize(cross(left, forward));
    return mat3(left,up,forward);
}

//main begin
void main() {
    //vec3 sphereColor = vec3(0.2,0.95,0.3);
    //ray.direction = rayDirection(75.0, u_resolution.xy, gl_FragCoord.xy);
    //vec3 eye = vec3(0,-0.2 + sin(u_time/3.0)*0.5, sin(u_time)*1.0 + 9.0);

    vec3 eye = vec3(0, 5.0, 0.0);
    vec3 at = vec3(0);
    eye.x += 14.7 * sin(u_time * 0.2);
    eye.z += 14.7 * cos(u_time * 0.2);
    vec2 uv = gl_FragCoord.xy/u_resolution.xy;
    uv = 2.0 * uv - 1.0;

    mat3 toWorld = setCamera(eye, at, 0.0);
    ray.direction = toWorld * normalize(vec3(uv,2.0));

    ray.magnitude = shortestDistanceToSurface(eye, ray.direction,  MIN_DIST, MAX_DIST);

    float noHit = step(ray.magnitude, MAX_DIST - EPSILON);

    vec3 pt = eye + ray.magnitude * ray.direction;


    //start lighting
    light mainlight;
    mainlight.pos = vec3(cos(u_time),0.5+(cos(u_time)*0.3),sin(u_time));
    mainlight.specularColor = vec3(0.9,0.9,0.9);
    mainlight.diffuseColor = vec3(0.75,0.75,0.75);
    mainlight.ambientColor = vec3(0.3,0.3,0.3);
    mainlight.intensity = 0.8;

    vec3 lightingColor = lightingStyle(pt, eye, mainlight, ray.hit);

    //the next line finds a vector between the light and the point on the
    //sphere
    //the next line finds the relationship between the normal of the point
    //and the light direction
    //max keeps the value zero if it's negative
    vec3 color = (1.0 - noHit)*background + (noHit*lightingColor);

    //cell shading
    //color = floor(color * 8.0)/8.0;

    gl_FragColor = vec4(color,1.0);
}
