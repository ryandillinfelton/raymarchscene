#ifdef GL_ES
precision mediump float;
#endif
#ifdef GL_OES_standard_derivatives
  #extension GL_EXT_shader_texture_lod : enable
  #extension GL_OES_standard_derivatives : enable
#endif

const int MAX_MARCHING_STEPS = 255;
const float MIN_DIST = 0.0;
const float MAX_DIST = 1500.0;
const float EPSILON = 0.0001;

uniform vec2 u_resolution;
uniform vec2 u_mouse;
uniform float u_time;
uniform vec3 eye;
uniform vec3 at;

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
    int hit;
    vec3 hitPoint;
} ray;

struct primitive {
    vec3 color;
    vec3 pos;
    float sdf;
};

//global background color declaration
vec3 background = vec3(0.75,0.8,0.95);

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

/*
    primitive for creating a box. The dimensions are reflected from the 1st
    quadrant to create the box.
*/
float sdBox( vec3 p, vec3 b )
{
  vec3 d = abs(p) - b;
  return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,vec3(0.0)));
}

/*
    primite for creating a torus. The first parameter is the position and the
    2nd controls the size of the torus and the inner circle.
*/
float sdTorus( vec3 p, vec2 t )
{
  vec2 q = vec2(length(p.xz)-t.x,p.y);
  return length(q)-t.y;
}

/*
    function for twisting specifically the torus. Increasing or decreasing the
    number inside the cos and sin will increase or decrease the amount of twist.
*/
float opTwistTorus( vec3 p )
{
    float c = cos(2.0*p.y);
    float s = sin(2.0*p.y);
    mat2  m = mat2(c,-s,s,c);
    vec3  q = vec3(m*p.xz,p.y);
    return sdTorus(q , vec2(1.0,0.25));
}

/*
    function for dispalcing pixels inside a primitve. This is called in
    sphereDisplace to change the sphere.
*/
float displacement(vec3 p){
  return sin(10.0*p.x)*sin(10.0*p.y)*sin(10.0*p.z);
}

/*
    function for displacing pixels of the sphere primitive. Can be called the
    same way the sdSphere function is called.
*/
float sphereDisplace( vec3 p, float s )
{
    float d1 = sdSphere(p, s);
    float d2 = displacement(p);
    return d1+d2;
}

/*
    function for unioning two objects in the scene.
*/
float un(float d1, float d2) {
    return min(d1,d2);
}

float sceneSDF(vec3 pt) {
    float floor = sdBox(pt-vec3(0,-1.7,0), vec3(30,0.0,20.0));
    float sphere = sdSphere(pt-vec3(sin(u_time),-0.5+(sin(u_time)*0.3),cos(u_time)), 0.5);
    float mirrorSphere = sdSphere(pt-vec3(2.811,1.0+(sin(u_time)*0.3),-3.0), 2.0);
    float mirrorCube = sdBox(pt-vec3(-3.5,0.869,-1.712), vec3(1.0,6.0,2.0));
    float twistTorus = opTwistTorus(pt- vec3(-1.5,0.5,-4.0));
    float dispSphere = sphereDisplace(pt - vec3(3.0, 3.0, 3.0), 2.0);


    float value = un(un(un(un(floor, sphere),un(mirrorCube, mirrorSphere)),twistTorus),dispSphere);

    if(value == floor) {
        ray.hit = 0;
    } else if (value == sphere || value == twistTorus || value == dispSphere) {
        ray.hit = 1;
    } else if (value == mirrorCube || value == mirrorSphere) {
        ray.hit = 2;
    }

    return value;
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

vec3 estimateNormal(vec3 p) {
    return normalize(vec3(
        sceneSDF(vec3(p.x + EPSILON, p.y, p.z)) - sceneSDF(vec3(p.x - EPSILON, p.y, p.z)),
        sceneSDF(vec3(p.x, p.y + EPSILON, p.z)) - sceneSDF(vec3(p.x, p.y - EPSILON, p.z)),
        sceneSDF(vec3(p.x, p.y, p.z  + EPSILON)) - sceneSDF(vec3(p.x, p.y, p.z - EPSILON))
    ));
}
//Calculates the diffused portion of the lighting
vec3 diffuseLighting(vec3 pt, light currentLight, vec3 normal) {
    vec3 lightDir = normalize(currentLight.pos-pt);
    float lDotn = dot(lightDir, normal);
    return (currentLight.diffuseColor * max(lDotn,0.0) * currentLight.intensity);
}
//Calculates the specular portion of the lighting
vec3 specularLighting(vec3 pt, vec3 normal, vec3 eye, light currentLight) {
    float shinyness = 8.0;
    vec3 l = normalize(currentLight.pos - pt);
    vec3 r = normalize(reflect(-l, normal));
    vec3 v = normalize(eye - pt);
    float rdotV = max(dot(r,v), 0.0);
    return (currentLight.specularColor * currentLight.intensity * pow(rdotV, shinyness));
}

float filterWidth(vec2 uv) {
  vec2 fw = max(abs(dFdx(uv)),abs(dFdy(uv)));
  return max(fw.x, fw.y);
}
//Creates the checkerboard pattern for the box acting as the floor
vec3 floorCheckerboard(vec3 pt) {
    vec3 color = vec3(0.981,0.985,0.995);

    float tile = floor(pt.x-(2.0 *floor(pt.x/2.0))) - floor(pt.z-(2.0*floor(pt.z/2.0)));
    if (tile < 0.0) {
        tile = 1.0;
    }
    return color * (1.0 - tile);
}
//Alternate way of creating the checkerboard pattern for the floor
vec3 floorCheckerboard2(vec2 uv){
  float width = filterWidth(uv);
  vec2 p0 = uv - 0.5 * width;
  vec2 p1 = uv + 0.5 * width;
  #define BUMPINT(x) (floor((x)/2.0) + 2.0 * max(((x)/2.0) - floor((x)/2.0) - 0.5, 0.0))
  vec2 i = (BUMPINT(p1) - BUMPINT(p0)) / width;
  float p = i.x * i.y + (1.0 - i.x) * (1.0 - i.y);
  return vec3(0.1+ 0.9 * p);
}
//Marches from the light to objects to see what parts of the scene should be in
//a shadow.
bool shadow(vec3 pt, light currentLight) {

    float dist = shortestDistanceToSurface(pt, normalize(currentLight.pos - pt), MIN_DIST+ 0.01, length(currentLight.pos - pt));

    bool hit;
    if (dist < length(currentLight.pos - pt)) {
        hit = true;
    } else if (dist == abs(length(currentLight.pos - pt))) {
        hit = false;
    }

    return  hit;
}
//Calls the other lighting functions to put together the full lighting display
vec3 lighting(vec3 pt, vec3 eye, light currentLight, vec3 objectColor) {
    vec3 ambientLight = currentLight.ambientColor;
    vec3 normal = estimateNormal(pt);

    vec3 diffuse = diffuseLighting(pt,currentLight, normal);
    vec3 specular = specularLighting(pt, normal, eye, currentLight);

    bool shadow = shadow(pt, currentLight);

    vec3 ptColor = (ambientLight + diffuse + specular) * objectColor;


    if (shadow) {
        ptColor = (float(shadow) * vec3(-0.1, -0.1, -0.1) + ambientLight) * objectColor;
    }

    return ptColor;
}
//Checks a color at a specific pixel
vec3 getObjectColor(vec3 pt) {
    vec3 sphereColor = vec3(0.830,0.164,0.276);
    vec3 floorColor = floorCheckerboard2(vec2(pt.x,pt.z));
    vec3 objectColor;
    if (ray.hit == 0) {
        objectColor = floorColor;
    } else if (ray.hit == 1) {
        objectColor = sphereColor;
    }

    return objectColor;
}
//Creates a mirror effect by checking the color of the point reflecting from
//one pixel out to another
vec3 mirror(vec3 pt, vec3 eye, light currentLight) {
    vec3 objectColor, normal, v, reflectedV, ogpt;
    float dist, noHit;
    vec3 mirrorColor = vec3(1.0, 1.0, 1.0);
    ogpt = pt;

    const int numBounces = 4;
    int bounces = 0;
    for (int i = 0; i < numBounces; i ++) {
        if(ray.hit == 0 || ray.hit == 1) {
            objectColor = lighting(pt, reflectedV, currentLight, getObjectColor(pt));
            break;
        }
        normal = estimateNormal(pt);
        v = eye - pt;
        reflectedV = normalize(reflect(-v, normal));
        dist = shortestDistanceToSurface(pt, reflectedV, MIN_DIST + 0.001, MAX_DIST);
        noHit = step(dist, MAX_DIST - EPSILON);

        pt += (normalize(reflectedV) * dist);
    }
    if (bounces == numBounces-1) {
        objectColor = lighting(pt, reflectedV, currentLight, vec3(0.6));
    }

    vec3 reflectionColor = ((1.0-noHit) * (background)) + (noHit * (objectColor));

    return lighting(ogpt, eye, currentLight, reflectionColor);
}
//Sets what type of lighting an object/pixel should have
vec3 lightingStyle(vec3 pt, vec3 eye, light currentLight) {
    vec3 color;
    vec3 objectColor = getObjectColor(pt);


    if (ray.hit == 0 || ray.hit == 1) {
        color = lighting(pt, eye, currentLight, objectColor);
    } else if (ray.hit == 2) {
        color = mirror(pt, eye, currentLight);
    }

    return color;
}


//lighting end

//Sets up the camera
mat3 setCamera(vec3 eye, vec3 center, float rotation) {
    vec3 forward = normalize(center - eye);
    vec3 orientation = vec3(sin(rotation),cos(rotation), 0.0);
    vec3 left = normalize(cross(forward,orientation));
    vec3 up = normalize(cross(left, forward));
    return mat3(left,up,forward);
}


//main begin
void main() {

    vec2 uv = gl_FragCoord.xy/u_resolution.xy;
    uv = 2.0 * uv - 1.0;

    mat3 toWorld = setCamera(eye, at, 0.0);
    ray.direction = toWorld * normalize(vec3(uv,2.0));

    ray.magnitude = shortestDistanceToSurface(eye, ray.direction,  MIN_DIST, MAX_DIST);

    float noHit = step(ray.magnitude, MAX_DIST - EPSILON);

    vec3 pt = eye + ray.magnitude * ray.direction;


    //start lighting
    light mainlight;
    mainlight.pos = vec3(0,6.5,0);
    mainlight.specularColor = vec3(0.9,0.9,0.9);
    mainlight.diffuseColor = vec3(0.7);
    mainlight.ambientColor = vec3(0.5);
    mainlight.intensity = 0.8;

    vec3 lightingColor = lightingStyle(pt, eye, mainlight);

    //the next line finds a vector between the light and the point on the
    //sphere
    //the next line finds the relationship between the normal of the point
    //and the light direction
    //max keeps the value zero if it's negative
    vec3 color = (1.0 - noHit)*background + (noHit*lightingColor);

    //cell shading
    //color = floor(color * 8.0)/8.0;
    #ifdef GL_OES_standard_derivatives
      gl_FragColor = vec4(color,1.0);
    #else
      gl_FragColor = vec4(0.0,0.0,0.0,1.0);
    #endif
}
